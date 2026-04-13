namespace BookStore.Models;

public class AdminBooksViewModel
{
    public IReadOnlyList<Book> Books { get; set; } = [];
    public IReadOnlyDictionary<int, string> CategoryNames { get; set; } = new Dictionary<int, string>();
}

