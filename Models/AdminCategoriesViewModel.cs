namespace BookStore.Models;

public class AdminCategoriesViewModel
{
    public IReadOnlyList<Category> Categories { get; set; } = [];
    public IReadOnlyDictionary<int, int> BookCounts { get; set; } = new Dictionary<int, int>();
    public AdminCategoryInputModel CreateInput { get; set; } = new();
}

