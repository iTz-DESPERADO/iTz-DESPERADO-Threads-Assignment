namespace TableBooking;

public sealed class FridayNightBooking
{
    public int TablesLeft { get; private set; } = 5;
    public int Booked { get; private set; }

    private readonly Lock @lock = new();

    public void Book(string customerName)
    {
        lock (@lock)
        {
            if (TablesLeft <= 0)
                return;

            //Thread.Sleep(20);   We can uncomment this line there is no problem but the best make the critical section become faster we don't need more delay here 

            TablesLeft--;
            Booked++;

            Console.WriteLine($"{customerName} booked a table. Left={TablesLeft}");
        }
    }
}
