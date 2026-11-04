using System.Threading.RateLimiting;
using DigitalLoppemarkedLib.Data.Repositories;
using DigitalLoppemarkedLib.Service;
using DigitalLoppemarkedWeb.Components;
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

        // Repositories: Handle all raw SQL queries.
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        // Services: Contain business logic and validation.
        builder.Services.AddScoped<CartService>();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<OrderService>();
        builder.Services.AddScoped<UserService>();

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
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}