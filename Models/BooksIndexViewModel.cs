namespace BookStore.Models;

public class BooksIndexViewModel
{
    public IReadOnlyList<Book> Books { get; set; } = [];
    public IReadOnlyList<Category> Categories { get; set; } = [];
    public int? SelectedCategoryId { get; set; }
    public string Keyword { get; set; } = string.Empty;
}

