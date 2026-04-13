namespace BookStore.Models;

public class BookDetailsViewModel
{
    public Book Book { get; set; } = new();
    public IReadOnlyList<BookReviewViewModel> Reviews { get; set; } = [];
    public AddReviewInputModel AddReviewInput { get; set; } = new();
    public bool CanReview { get; set; }
    public int TotalReviews => Reviews.Count;
    public decimal AverageRating => TotalReviews == 0
        ? 0
        : Math.Round((decimal)Reviews.Average(x => x.Rating), 1);
}
