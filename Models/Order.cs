namespace BookStore.Models;

public class Order
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = PaymentMethods.CashOnDelivery;
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime? PaidAt { get; set; }
    public string? PaymentNote { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

