namespace BookStore.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
}

