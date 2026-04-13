namespace BookStore.Models;

public class AdminDashboardViewModel
{
    public int TotalBooks { get; set; }
    public int TotalCategories { get; set; }
    public int TotalOrders { get; set; }
    public int TodayOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public IReadOnlyList<Book> LowStockBooks { get; set; } = [];
    public IReadOnlyList<Order> RecentOrders { get; set; } = [];
}

