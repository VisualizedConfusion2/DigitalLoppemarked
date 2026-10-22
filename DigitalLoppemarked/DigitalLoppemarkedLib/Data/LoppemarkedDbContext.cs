using DigitalLoppemarkedLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalLoppemarkedLib.Data;

public class LoppemarkedDbContext : DbContext
{
    public LoppemarkedDbContext(DbContextOptions<LoppemarkedDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.Id);

            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasColumnName("description").IsRequired();
            e.Property(p => p.Price).HasColumnName("price").HasPrecision(10, 2);
            e.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(500).IsRequired();
            e.Property(p => p.StockQuantity).HasColumnName("stock_quantity");
            e.Property(p => p.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(o => o.Id);

            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            e.Property(o => o.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(o => o.ShippingAddress).HasColumnName("shipping_address").HasMaxLength(300);
            e.Property(o => o.City).HasColumnName("city").HasMaxLength(100);
            e.Property(o => o.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
            e.Property(o => o.Country).HasColumnName("country").HasMaxLength(100);
            e.Property(o => o.OrderDate).HasColumnName("order_date");
            e.Property(o => o.Status).HasColumnName("status").HasColumnType("order_status");

            // Computed in C#, not stored
            e.Ignore(o => o.Total);

            e.HasMany(o => o.Lines)
             .WithOne()
             .HasForeignKey("OrderId")
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.ToTable("order_lines");

            // OrderLine has no Id or OrderId in the model, so these are
            // shadow properties. The table's primary key and foreign key
            // are still mapped without changing the class.
            e.Property<int>("Id").HasColumnName("id").ValueGeneratedOnAdd();
            e.HasKey("Id");

            e.Property<int>("OrderId").HasColumnName("order_id");

            e.Property(l => l.ProductId).HasColumnName("product_id");
            e.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            e.Property(l => l.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2);
            e.Property(l => l.Quantity).HasColumnName("quantity");

            // Computed in C#, not stored
            e.Ignore(l => l.Subtotal);

            e.HasOne<Product>()
             .WithMany()
             .HasForeignKey(l => l.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}