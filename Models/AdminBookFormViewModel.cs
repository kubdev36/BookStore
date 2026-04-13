namespace BookStore.Models;

public class AdminBookFormViewModel
{
    public AdminBookInputModel Input { get; set; } = new();
    public IReadOnlyList<Category> Categories { get; set; } = [];
    public bool IsEdit { get; set; }
    public int BookId { get; set; }
}

