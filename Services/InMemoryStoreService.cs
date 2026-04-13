using System.Security.Cryptography;
using BookStore.Models;

namespace BookStore.Services;

public class InMemoryStoreService : IStoreService
{
    private readonly object _lock = new();
    private readonly List<UserAccount> _users;
    private readonly List<Category> _categories;
    private readonly List<Book> _books;
    private readonly List<Order> _orders;
    private readonly List<ReviewRecord> _reviews;
    private readonly List<ReviewLikeRecord> _reviewLikes;
    private readonly List<ReviewReplyRecord> _reviewReplies;
    private readonly List<CartItemRecord> _cartItems;

    public InMemoryStoreService()
    {
        _users = [];

        SeedAdminAccount();

        _categories =
        [
            new Category { Id = 1, Name = "Lap trinh" },
            new Category { Id = 2, Name = "Kinh te" },
            new Category { Id = 3, Name = "Phat trien ban than" },
            new Category { Id = 4, Name = "Van hoc" }
        ];

        _books =
        [
            new Book
            {
                Id = 1,
                Title = "Clean Code",
                Author = "Robert C. Martin",
                CategoryId = 1,
                Price = 259000,
                Stock = 20,
                Description = "Sach kinh dien ve ky thuat viet code de doc, de bao tri.",
                CoverUrl = "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=500"
            },
            new Book
            {
                Id = 2,
                Title = "The Pragmatic Programmer",
                Author = "Andrew Hunt",
                CategoryId = 1,
                Price = 299000,
                Stock = 15,
                Description = "Huong dan tu duy va ky nang lap trinh thuc te.",
                CoverUrl = "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=500"
            },
            new Book
            {
                Id = 3,
                Title = "Nha Gia Kim",
                Author = "Paulo Coelho",
                CategoryId = 4,
                Price = 99000,
                Stock = 30,
                Description = "Tieu thuyet truyen cam hung noi tieng tren toan the gioi.",
                CoverUrl = "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=500"
            },
            new Book
            {
                Id = 4,
                Title = "Atomic Habits",
                Author = "James Clear",
                CategoryId = 3,
                Price = 189000,
                Stock = 18,
                Description = "Phuong phap xay dung thoi quen tot va loai bo thoi quen xau.",
                CoverUrl = "https://images.unsplash.com/photo-1516979187457-637abb4f9353?w=500"
            }
        ];

        _orders = [];
        _reviews = [];
        _reviewLikes = [];
        _reviewReplies = [];
        _cartItems = [];
    }

    public (bool Success, string Message, UserAccount? User) RegisterUser(RegisterInputModel input, string role = UserRoles.User)
    {
        lock (_lock)
        {
            var normalizedEmail = NormalizeEmail(input.Email);
            var exists = _users.Any(x => x.Email == normalizedEmail);
            if (exists)
            {
                return (false, "Email da duoc dang ky.", null);
            }

            var (hash, salt) = HashPassword(input.Password);
            var nextId = _users.Count == 0 ? 1 : _users.Max(x => x.Id) + 1;
            var user = new UserAccount
            {
                Id = nextId,
                FullName = input.FullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            _users.Add(user);
            return (true, "Dang ky thanh cong.", user);
        }
    }

    public UserAccount? ValidateUser(string email, string password)
    {
        lock (_lock)
        {
            var normalizedEmail = NormalizeEmail(email);
            var user = _users.FirstOrDefault(x => x.Email == normalizedEmail);
            if (user is null)
            {
                return null;
            }

            return VerifyPassword(password, user.PasswordHash, user.PasswordSalt) ? user : null;
        }
    }

    public UserAccount? GetUserByEmail(string email)
    {
        lock (_lock)
        {
            var normalizedEmail = NormalizeEmail(email);
            return _users.FirstOrDefault(x => x.Email == normalizedEmail);
        }
    }

    public IReadOnlyList<Category> GetCategories()
    {
        lock (_lock)
        {
            return _categories.OrderBy(x => x.Name).ToList();
        }
    }

    public IReadOnlyDictionary<int, int> GetBookCountByCategory()
    {
        lock (_lock)
        {
            return _books
                .GroupBy(x => x.CategoryId)
                .ToDictionary(x => x.Key, x => x.Count());
        }
    }

    public IReadOnlyList<Book> GetBooks(int? categoryId = null, string? keyword = null)
    {
        lock (_lock)
        {
            var query = _books.AsEnumerable();

            if (categoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderBy(x => x.Title).ToList();
        }
    }

    public Book? GetBook(int id)
    {
        lock (_lock)
        {
            return _books.FirstOrDefault(x => x.Id == id);
        }
    }

    public bool HasUserPurchasedBook(int userId, int bookId)
    {
        lock (_lock)
        {
            return _orders.Any(x => x.UserId == userId && x.Items.Any(i => i.BookId == bookId));
        }
    }

    public IReadOnlyList<BookReviewViewModel> GetBookReviews(int bookId, int? currentUserId = null)
    {
        lock (_lock)
        {
            var reviewQuery = _reviews
                .Where(x => x.BookId == bookId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var mapped = new List<BookReviewViewModel>(reviewQuery.Count);
            foreach (var review in reviewQuery)
            {
                var user = _users.FirstOrDefault(x => x.Id == review.UserId);
                var likeCount = _reviewLikes.Count(x => x.ReviewId == review.Id);
                var likedByCurrent = currentUserId.HasValue && _reviewLikes.Any(x => x.ReviewId == review.Id && x.UserId == currentUserId.Value);
                var reply = _reviewReplies.FirstOrDefault(x => x.ReviewId == review.Id);
                var adminName = reply is null
                    ? null
                    : _users.FirstOrDefault(x => x.Id == reply.AdminUserId)?.FullName;

                mapped.Add(new BookReviewViewModel
                {
                    Id = review.Id,
                    BookId = review.BookId,
                    UserId = review.UserId,
                    UserFullName = user?.FullName ?? $"Người dùng #{review.UserId}",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    ImageUrl = review.ImageUrl,
                    CreatedAt = review.CreatedAt,
                    LikeCount = likeCount,
                    IsLikedByCurrentUser = likedByCurrent,
                    AdminReply = reply?.ReplyContent,
                    AdminReplyBy = adminName,
                    AdminReplyAt = reply?.RepliedAt
                });
            }

            return mapped;
        }
    }

    public (bool Success, string Message) AddBookReview(int bookId, int userId, int rating, string comment, string? imageUrl)
    {
        lock (_lock)
        {
            if (!_books.Any(x => x.Id == bookId))
            {
                return (false, "Không tìm thấy sách cần đánh giá.");
            }

            if (!_users.Any(x => x.Id == userId))
            {
                return (false, "Không tìm thấy người dùng.");
            }

            if (!HasUserPurchasedBook(userId, bookId))
            {
                return (false, "Bạn cần mua sách này trước khi gửi đánh giá.");
            }

            if (rating is < 1 or > 5)
            {
                return (false, "Số sao đánh giá không hợp lệ.");
            }

            var normalizedComment = (comment ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedComment))
            {
                return (false, "Vui lòng nhập nội dung đánh giá.");
            }

            var nextId = _reviews.Count == 0 ? 1 : _reviews.Max(x => x.Id) + 1;
            _reviews.Add(new ReviewRecord
            {
                Id = nextId,
                BookId = bookId,
                UserId = userId,
                Rating = rating,
                Comment = normalizedComment,
                ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            return (true, "Đã gửi đánh giá của bạn.");
        }
    }

    public (bool Success, string Message) ToggleBookReviewLike(int reviewId, int userId)
    {
        lock (_lock)
        {
            if (!_reviews.Any(x => x.Id == reviewId))
            {
                return (false, "Không tìm thấy bình luận.");
            }

            var like = _reviewLikes.FirstOrDefault(x => x.ReviewId == reviewId && x.UserId == userId);
            if (like is not null)
            {
                _reviewLikes.Remove(like);
                return (true, "Đã bỏ tim bình luận.");
            }

            _reviewLikes.Add(new ReviewLikeRecord
            {
                ReviewId = reviewId,
                UserId = userId
            });
            return (true, "Đã tim bình luận.");
        }
    }

    public (bool Success, string Message) ReplyToBookReview(int reviewId, int adminUserId, string replyContent)
    {
        lock (_lock)
        {
            var admin = _users.FirstOrDefault(x => x.Id == adminUserId && x.Role == UserRoles.Admin);
            if (admin is null)
            {
                return (false, "Bạn không có quyền phản hồi bình luận.");
            }

            if (!_reviews.Any(x => x.Id == reviewId))
            {
                return (false, "Không tìm thấy bình luận cần phản hồi.");
            }

            var normalized = (replyContent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return (false, "Nội dung phản hồi không được để trống.");
            }

            var existing = _reviewReplies.FirstOrDefault(x => x.ReviewId == reviewId);
            if (existing is null)
            {
                _reviewReplies.Add(new ReviewReplyRecord
                {
                    ReviewId = reviewId,
                    AdminUserId = adminUserId,
                    ReplyContent = normalized,
                    RepliedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.AdminUserId = adminUserId;
                existing.ReplyContent = normalized;
                existing.RepliedAt = DateTime.UtcNow;
            }

            return (true, "Đã phản hồi bình luận.");
        }
    }

    public Book AddBook(AdminBookInputModel input)
    {
        lock (_lock)
        {
            var nextId = _books.Count == 0 ? 1 : _books.Max(x => x.Id) + 1;
            var item = new Book
            {
                Id = nextId,
                Title = input.Title,
                Author = input.Author,
                Price = input.Price,
                Stock = input.Stock,
                CategoryId = input.CategoryId,
                Description = input.Description,
                CoverUrl = input.CoverUrl
            };

            _books.Add(item);
            return item;
        }
    }

    public bool UpdateBook(int id, AdminBookInputModel input)
    {
        lock (_lock)
        {
            var item = _books.FirstOrDefault(x => x.Id == id);
            if (item is null)
            {
                return false;
            }

            item.Title = input.Title;
            item.Author = input.Author;
            item.Price = input.Price;
            item.Stock = input.Stock;
            item.CategoryId = input.CategoryId;
            item.Description = input.Description;
            item.CoverUrl = input.CoverUrl;

            return true;
        }
    }

    public bool DeleteBook(int id)
    {
        lock (_lock)
        {
            var item = _books.FirstOrDefault(x => x.Id == id);
            if (item is null)
            {
                return false;
            }

            var inOrder = _orders.Any(x => x.Items.Any(i => i.BookId == id));
            if (inOrder)
            {
                return false;
            }

            _books.Remove(item);
            return true;
        }
    }

    public Category AddCategory(AdminCategoryInputModel input)
    {
        lock (_lock)
        {
            var normalized = input.Name.Trim();
            var duplicate = _categories.Any(x => x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                throw new InvalidOperationException("Danh muc da ton tai.");
            }

            var nextId = _categories.Count == 0 ? 1 : _categories.Max(x => x.Id) + 1;
            var category = new Category
            {
                Id = nextId,
                Name = normalized
            };

            _categories.Add(category);
            return category;
        }
    }

    public bool UpdateCategory(int id, AdminCategoryInputModel input)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(x => x.Id == id);
            if (category is null)
            {
                return false;
            }

            var normalized = input.Name.Trim();
            var duplicate = _categories.Any(x => x.Id != id && x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                throw new InvalidOperationException("Danh muc da ton tai.");
            }

            category.Name = normalized;
            return true;
        }
    }

    public (bool Success, string Message) DeleteCategory(int id)
    {
        lock (_lock)
        {
            var category = _categories.FirstOrDefault(x => x.Id == id);
            if (category is null)
            {
                return (false, "Khong tim thay danh muc.");
            }

            var hasBooks = _books.Any(x => x.CategoryId == id);
            if (hasBooks)
            {
                return (false, "Danh muc dang co sach, khong the xoa.");
            }

            _categories.Remove(category);
            return (true, "Da xoa danh muc.");
        }
    }

    public IReadOnlyList<Order> GetOrders()
    {
        lock (_lock)
        {
            return _orders
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }
    }

    public Order? GetOrder(int id)
    {
        lock (_lock)
        {
            return _orders.FirstOrDefault(x => x.Id == id);
        }
    }

    public int CreateOrder(CheckoutInputModel input, IReadOnlyList<CartItemViewModel> items, int? userId = null, string? paymentNote = null)
    {
        lock (_lock)
        {
            foreach (var line in items)
            {
                var book = _books.FirstOrDefault(x => x.Id == line.BookId);
                if (book is null || line.Quantity > book.Stock)
                {
                    throw new InvalidOperationException($"Sach '{line.Title}' da het hoac khong du so luong ton.");
                }
            }

            var nextId = _orders.Count == 0 ? 1 : _orders.Max(x => x.Id) + 1;
            var order = new Order
            {
                Id = nextId,
                UserId = userId,
                CustomerName = input.CustomerName,
                Email = input.Email,
                Phone = input.Phone,
                Address = input.Address,
                PaymentMethod = string.IsNullOrWhiteSpace(input.PaymentMethod) ? PaymentMethods.CashOnDelivery : input.PaymentMethod,
                PaymentStatus = string.Equals(input.PaymentMethod, PaymentMethods.VietQr, StringComparison.OrdinalIgnoreCase) ? "PendingPayment" : "Pending",
                PaymentNote = paymentNote,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = items.Sum(x => x.LineTotal),
                Items = items.Select(x => new OrderItem
                {
                    BookId = x.BookId,
                    BookTitle = x.Title,
                    UnitPrice = x.Price,
                    Quantity = x.Quantity
                }).ToList()
            };

            foreach (var line in items)
            {
                var book = _books.First(x => x.Id == line.BookId);
                book.Stock -= line.Quantity;
            }

            _orders.Add(order);
            return order.Id;
        }
    }

    public IReadOnlyList<CartItemViewModel> GetCartItems(int userId)
    {
        lock (_lock)
        {
            var result = new List<CartItemViewModel>();
            var items = _cartItems.Where(x => x.UserId == userId).ToList();
            foreach (var item in items)
            {
                var book = _books.FirstOrDefault(b => b.Id == item.BookId);
                if (book != null)
                {
                    result.Add(new CartItemViewModel
                    {
                        BookId = book.Id,
                        Title = book.Title,
                        CoverUrl = book.CoverUrl,
                        Price = book.Price,
                        Quantity = item.Quantity
                    });
                }
            }
            return result;
        }
    }

    public void SyncCartItems(int userId, IEnumerable<CartItemViewModel> items)
    {
        lock (_lock)
        {
            _cartItems.RemoveAll(x => x.UserId == userId);
            var nextId = _cartItems.Count == 0 ? 1 : _cartItems.Max(x => x.Id) + 1;
            foreach (var item in items)
            {
                _cartItems.Add(new CartItemRecord
                {
                    Id = nextId++,
                    UserId = userId,
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
    }

    public void ClearCart(int userId)
    {
        lock (_lock)
        {
            _cartItems.RemoveAll(x => x.UserId == userId);
        }
    }

    private void SeedAdminAccount()
    {
        var register = new RegisterInputModel
        {
            FullName = "System Admin",
            Email = "admin@bookstore.local",
            Password = "Admin@123",
            ConfirmPassword = "Admin@123"
        };

        RegisterUser(register, UserRoles.Admin);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashedInput = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        var expectedHash = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(hashedInput, expectedHash);
    }

    private sealed class ReviewRecord
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int UserId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ReviewLikeRecord
    {
        public int ReviewId { get; set; }
        public int UserId { get; set; }
    }

    private sealed class ReviewReplyRecord
    {
        public int ReviewId { get; set; }
        public int AdminUserId { get; set; }
        public string ReplyContent { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; }
    }

    private sealed class CartItemRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
