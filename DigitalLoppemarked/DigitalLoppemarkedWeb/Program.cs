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

        // Connection string fra user secrets (Development) eller miljøvariabler (produktion)
        var connString = builder.Configuration.GetConnectionString("Loppemarked")
            ?? throw new InvalidOperationException(
                "Connection string 'Loppemarked' mangler. Kør dotnet user-secrets set i web-projektet.");

        // Én connection pool til hele appen
        builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connString).Build());

// ... (keep imports)

// ... (rest)

// Inside Main method:
        // Repositories (SQL ligger kun her)
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        // Services (kun validering og forretningslogik)
        builder.Services.AddScoped<CartService>();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<OrderService>();
        builder.Services.AddScoped<UserService>();


        // Rate limiting to curb scraping / DoS of the browse endpoints (story 3).
        // Fixed window per client IP; excess requests get HTTP 429.
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

            // Dedicated, tighter policy for search (story 7). It guards the HTTP-rendered
            // search path; interactive searches over the SignalR circuit are additionally
            // throttled per-circuit inside Home.razor, since those don't flow through this
            // request-pipeline middleware.
            options.AddFixedWindowLimiter("search", o =>
            {
                o.PermitLimit = 20;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 0;
            });
        });

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRateLimiter();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}