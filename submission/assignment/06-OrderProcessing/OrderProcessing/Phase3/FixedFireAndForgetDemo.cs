using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class FixedFireAndForgetDemo(OrderRepository orderRepository, IEmailService emailService)
{
    public async Task ProcessOrderAsync(Order order, Customer customer, CancellationToken ct)
    {
        await orderRepository.SaveAsync(order, ct);

        await emailService.SendConfirmationAsync(order, customer, ct);

        Console.WriteLine("Order processing finished");
    }
}
