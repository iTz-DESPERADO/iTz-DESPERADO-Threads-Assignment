using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class CustomerService(CustomerRepository customers)
{
    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
    {
        return await customers.GetByIdAsync(id, ct)
            ?? throw new CustomerNotFoundException($"Customer #{id} not found");
    }
}
