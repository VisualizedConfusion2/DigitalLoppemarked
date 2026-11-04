namespace DigitalLoppemarkedLib.Data;

public sealed class DataAccessException : Exception
{
    public DataAccessException(string message, Exception? inner = null)
        : base(message, inner) { }
}

public sealed class InsufficientStockException : Exception
{
    public int ProductId { get; }

    public InsufficientStockException(int productId)
        : base($"Ikke nok lager for produkt {productId}.")
    {
        ProductId = productId;
    }
}