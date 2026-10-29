namespace OrderProcessing.Shared.Models;
public class CustomerDashboard
{
    public Customer Customer { get; set; } = new();
    public List<Order> Orders { get; set; } = [];
    public List<PaymentTransaction> Payments { get; set; } = [];
    public List<EmailMessage> Emails { get; set; } = [];
}
