using System.Threading.RateLimiting;
using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Service;
using DigitalLoppemarkedWeb.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
namespace DigitalLoppemarkedWeb;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Connection string from user secrets (Development) or environment variables (production)
        var connString = builder.Configuration.GetConnectionString("Loppemarked")
            ?? throw new InvalidOperationException(
                "Connection string 'Loppemarked' is missing. Run dotnet user-secrets set in the web project.");

        // One connection pool for the entire app
        builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connString).Build());

        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // Signs the short-lived post-login ticket used to regenerate the session id (story 2).
        builder.Services.AddDataProtection();

        // Repositories: Handle all raw SQL queries.
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        // Services: Contain business logic and validation.
        builder.Services.AddScoped<CartService>();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<OrderService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<IEmailService, ConsoleEmailService>();


        // Rate limiting to prevent scraping / DoS (story 3).
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Specific policy for search (story 7).
            options.AddFixedWindowLimiter("search", o =>
            {
                o.PermitLimit = 20;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 0;
            });
        });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseSession();

        // Session-fixation mitigation: after the login redirect the old session cookie is
        // gone, so this request gets a fresh session id. Populate it once from the signed
        // "post_login" ticket, then delete the ticket.
        app.Use(async (context, next) =>
        {
            const string ticketCookie = "post_login";
            if (context.Session.GetInt32("UserId") is null &&
                context.Request.Cookies.TryGetValue(ticketCookie, out var ticket) &&
                !string.IsNullOrEmpty(ticket))
            {
                try
                {
                    var protector = context.RequestServices
                        .GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector("LoginTicket");
                    context.Session.SetInt32("UserId", int.Parse(protector.Unprotect(ticket)));
                }
                catch
                {
                    // Tampered or expired ticket: ignore and remain logged out.
                }
                context.Response.Cookies.Delete(ticketCookie);
            }
            await next();
        });

        app.UseAntiforgery();

        // Security Stamp Verification Middleware: Invalidate sessions globally if stamp changes
        app.Use(async (context, next) =>
        {
            var userId = context.Session.GetInt32("UserId");
            var sessionStamp = context.Session.GetString("SecurityStamp");

            if (userId.HasValue && !string.IsNullOrEmpty(sessionStamp))
            {
                var userService = context.RequestServices.GetRequiredService<UserService>();
                var userRepo = context.RequestServices.GetRequiredService<IUserRepository>();

                var user = await userRepo.GetByIdAsync(userId.Value, default);
                if (user == null || user.SecurityStamp != sessionStamp)
                {
                    context.Session.Clear();
                    // Note: Redirection is handled by the UI/Components if they detect session loss
                }
            }
            await next();
        });

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}