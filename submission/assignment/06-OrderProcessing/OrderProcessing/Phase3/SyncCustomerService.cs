using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
namespace OrderProcessing.Phase3;

public sealed class SyncCustomerService(SyncCustomerRepository customers)
{
    public Customer GetCustomer(int id, CancellationToken ct) => customers.GetById(id, ct)
        ?? throw new CustomerNotFoundException($"Customer #{id} not found");
}
