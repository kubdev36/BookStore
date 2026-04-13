namespace BookStore.Models;

public class BookReviewViewModel
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public string? AdminReply { get; set; }
    public string? AdminReplyBy { get; set; }
    public DateTime? AdminReplyAt { get; set; }
}
