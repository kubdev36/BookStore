namespace BookStore.Models;

public class OrderItem
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

