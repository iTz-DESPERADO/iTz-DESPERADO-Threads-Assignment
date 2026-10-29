namespace OrderProcessing.Shared.Models;
public class ShippingQuote
{
    public string Carrier { get; set; } = "";
    public decimal Price { get; set; }
    public long ElapsedMs { get; set; }
}
