namespace DigitalLoppemarkedLib.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public int StockQuantity { get; set; }
    

    // Only active products are shown in the shop. Inactive products must not be
    // reachable by guessing their ID (story 3: "Adgang til skjulte eller inaktive
    // produkter via manipulation af ID'er forhindres").
    public bool IsActive { get; set; } = true;
}