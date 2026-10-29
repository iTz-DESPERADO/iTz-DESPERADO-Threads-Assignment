using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class BrokenFireAndForgetDemo(OrderRepository orderRepository, IEmailService emailService)
{
    private readonly OrderRepository _orderRepository = orderRepository;
    
    private readonly IEmailService _emailService = emailService;

    public async Task ProcessOrderAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);

#pragma warning disable CS4014
        _emailService.SendConfirmationAsync(order, customer);   // not awaited
#pragma warning restore CS4014

        Console.WriteLine("Order processing finished");
    }
}
