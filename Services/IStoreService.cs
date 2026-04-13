using BookStore.Models;

namespace BookStore.Services;

public interface IStoreService
{
    (bool Success, string Message, UserAccount? User) RegisterUser(RegisterInputModel input, string role = UserRoles.User);
    UserAccount? ValidateUser(string email, string password);
    UserAccount? GetUserByEmail(string email);

    IReadOnlyList<Category> GetCategories();
    IReadOnlyDictionary<int, int> GetBookCountByCategory();
    IReadOnlyList<Book> GetBooks(int? categoryId = null, string? keyword = null);
    Book? GetBook(int id);
    bool HasUserPurchasedBook(int userId, int bookId);
    IReadOnlyList<BookReviewViewModel> GetBookReviews(int bookId, int? currentUserId = null);
    (bool Success, string Message) AddBookReview(int bookId, int userId, int rating, string comment, string? imageUrl);
    (bool Success, string Message) ToggleBookReviewLike(int reviewId, int userId);
    (bool Success, string Message) ReplyToBookReview(int reviewId, int adminUserId, string replyContent);
    Book AddBook(AdminBookInputModel input);
    bool UpdateBook(int id, AdminBookInputModel input);
    bool DeleteBook(int id);
    Category AddCategory(AdminCategoryInputModel input);
    bool UpdateCategory(int id, AdminCategoryInputModel input);
    (bool Success, string Message) DeleteCategory(int id);
    IReadOnlyList<Order> GetOrders();
    Order? GetOrder(int id);
    int CreateOrder(CheckoutInputModel input, IReadOnlyList<CartItemViewModel> items, int? userId = null, string? paymentNote = null);
    IReadOnlyList<CartItemViewModel> GetCartItems(int userId);
    void SyncCartItems(int userId, IEnumerable<CartItemViewModel> items);
    void ClearCart(int userId);
}
