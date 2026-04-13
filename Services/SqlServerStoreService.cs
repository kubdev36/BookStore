using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using BookStore.Models;

namespace BookStore.Services;

public class SqlServerStoreService : IStoreService
{
    private readonly string _connectionString;

    public SqlServerStoreService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Missing connection string 'SqlServer'.");
    }

    public (bool Success, string Message, UserAccount? User) RegisterUser(RegisterInputModel input, string role = UserRoles.User)
    {
        var normalizedEmail = NormalizeEmail(input.Email);

        using var connection = CreateConnection();
        connection.Open();

        using (var checkCmd = new SqlCommand(@"
SELECT TOP 1 id
FROM user_accounts
WHERE LOWER(email) = @email;", connection))
        {
            checkCmd.Parameters.AddWithValue("@email", normalizedEmail);
            var exists = checkCmd.ExecuteScalar();
            if (exists is not null)
            {
                return (false, "Email da duoc dang ky.", null);
            }
        }

        var (hash, salt) = HashPassword(input.Password);

        using var insertCmd = new SqlCommand(@"
INSERT INTO user_accounts (full_name, email, password_hash, password_salt, role)
OUTPUT INSERTED.id, INSERTED.full_name, INSERTED.email, INSERTED.password_hash, INSERTED.password_salt, INSERTED.role, INSERTED.created_at
VALUES (@fullName, @email, @passwordHash, @passwordSalt, @role);", connection);

        insertCmd.Parameters.AddWithValue("@fullName", input.FullName.Trim());
        insertCmd.Parameters.AddWithValue("@email", normalizedEmail);
        insertCmd.Parameters.AddWithValue("@passwordHash", hash);
        insertCmd.Parameters.AddWithValue("@passwordSalt", salt);
        insertCmd.Parameters.AddWithValue("@role", role);

        using var reader = insertCmd.ExecuteReader();
        if (!reader.Read())
        {
            return (false, "Khong the tao tai khoan.", null);
        }

        var user = MapUser(reader);
        return (true, "Dang ky thanh cong.", user);
    }

    public UserAccount? ValidateUser(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT TOP 1 id, full_name, email, password_hash, password_salt, role, created_at
FROM user_accounts
WHERE LOWER(email) = @email;", connection);

        cmd.Parameters.AddWithValue("@email", normalizedEmail);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var user = MapUser(reader);
        return VerifyPassword(password, user.PasswordHash, user.PasswordSalt) ? user : null;
    }

    public UserAccount? GetUserByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT TOP 1 id, full_name, email, password_hash, password_salt, role, created_at
FROM user_accounts
WHERE LOWER(email) = @email;", connection);

        cmd.Parameters.AddWithValue("@email", normalizedEmail);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public IReadOnlyList<Category> GetCategories()
    {
        var results = new List<Category>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT id, name
FROM categories
ORDER BY name;", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return results;
    }

    public IReadOnlyDictionary<int, int> GetBookCountByCategory()
    {
        var dict = new Dictionary<int, int>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT category_id, COUNT(*) AS total
FROM books
GROUP BY category_id;", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dict[reader.GetInt32(0)] = reader.GetInt32(1);
        }

        return dict;
    }

    public IReadOnlyList<Book> GetBooks(int? categoryId = null, string? keyword = null)
    {
        var books = new List<Book>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand();
        cmd.Connection = connection;

        var sql = @"
SELECT id, title, author, price, stock, category_id, description, cover_url
FROM books
WHERE 1 = 1";

        if (categoryId.HasValue)
        {
            sql += " AND category_id = @categoryId";
            cmd.Parameters.AddWithValue("@categoryId", categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += " AND (title LIKE @keyword OR author LIKE @keyword)";
            cmd.Parameters.AddWithValue("@keyword", $"%{keyword.Trim()}%");
        }

        sql += " ORDER BY title;";
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            books.Add(MapBook(reader));
        }

        return books;
    }

    public Book? GetBook(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT TOP 1 id, title, author, price, stock, category_id, description, cover_url
FROM books
WHERE id = @id;", connection);

        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapBook(reader) : null;
    }

    public bool HasUserPurchasedBook(int userId, int bookId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.orders o
INNER JOIN dbo.order_items oi ON oi.order_id = o.id
WHERE o.user_id = @userId
  AND oi.book_id = @bookId;", connection);

        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@bookId", bookId);

        return cmd.ExecuteScalar() is not null;
    }

    public IReadOnlyList<BookReviewViewModel> GetBookReviews(int bookId, int? currentUserId = null)
    {
        var reviews = new List<BookReviewViewModel>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT
    r.id,
    r.book_id,
    r.user_id,
    u.full_name,
    r.rating,
    r.comment,
    r.image_url,
    r.created_at,
    ISNULL(lc.like_count, 0) AS like_count,
    CASE WHEN rl.user_id IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS is_liked,
    rr.reply_content,
    au.full_name AS admin_reply_by,
    rr.replied_at
FROM dbo.book_reviews r
INNER JOIN dbo.user_accounts u ON u.id = r.user_id
LEFT JOIN (
    SELECT review_id, COUNT(*) AS like_count
    FROM dbo.book_review_likes
    GROUP BY review_id
) lc ON lc.review_id = r.id
LEFT JOIN dbo.book_review_likes rl
    ON rl.review_id = r.id
   AND rl.user_id = @currentUserId
LEFT JOIN dbo.book_review_replies rr ON rr.review_id = r.id
LEFT JOIN dbo.user_accounts au ON au.id = rr.admin_user_id
WHERE r.book_id = @bookId
ORDER BY r.created_at DESC;", connection);

        cmd.Parameters.AddWithValue("@bookId", bookId);
        var currentUserIdParam = cmd.Parameters.Add("@currentUserId", SqlDbType.Int);
        currentUserIdParam.Value = currentUserId.HasValue ? currentUserId.Value : DBNull.Value;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            reviews.Add(new BookReviewViewModel
            {
                Id = reader.GetInt32(0),
                BookId = reader.GetInt32(1),
                UserId = reader.GetInt32(2),
                UserFullName = reader.GetString(3),
                Rating = reader.GetInt32(4),
                Comment = reader.GetString(5),
                ImageUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                LikeCount = reader.GetInt32(8),
                IsLikedByCurrentUser = reader.GetBoolean(9),
                AdminReply = reader.IsDBNull(10) ? null : reader.GetString(10),
                AdminReplyBy = reader.IsDBNull(11) ? null : reader.GetString(11),
                AdminReplyAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
            });
        }

        return reviews;
    }

    public (bool Success, string Message) AddBookReview(int bookId, int userId, int rating, string comment, string? imageUrl)
    {
        if (rating is < 1 or > 5)
        {
            return (false, "Số sao đánh giá không hợp lệ.");
        }

        var normalizedComment = (comment ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            return (false, "Vui lòng nhập nội dung đánh giá.");
        }

        using var connection = CreateConnection();
        connection.Open();

        using (var checkBookCmd = new SqlCommand("SELECT TOP 1 1 FROM dbo.books WHERE id = @bookId;", connection))
        {
            checkBookCmd.Parameters.AddWithValue("@bookId", bookId);
            if (checkBookCmd.ExecuteScalar() is null)
            {
                return (false, "Không tìm thấy sách cần đánh giá.");
            }
        }

        using (var checkUserCmd = new SqlCommand("SELECT TOP 1 1 FROM dbo.user_accounts WHERE id = @userId;", connection))
        {
            checkUserCmd.Parameters.AddWithValue("@userId", userId);
            if (checkUserCmd.ExecuteScalar() is null)
            {
                return (false, "Không tìm thấy người dùng.");
            }
        }

        if (!HasUserPurchasedBook(userId, bookId))
        {
            return (false, "Bạn cần mua sách này trước khi gửi đánh giá.");
        }

        using var cmd = new SqlCommand(@"
INSERT INTO dbo.book_reviews (book_id, user_id, rating, comment, image_url)
VALUES (@bookId, @userId, @rating, @comment, @imageUrl);", connection);

        cmd.Parameters.AddWithValue("@bookId", bookId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@rating", rating);
        cmd.Parameters.AddWithValue("@comment", normalizedComment);
        cmd.Parameters.AddWithValue("@imageUrl", string.IsNullOrWhiteSpace(imageUrl) ? DBNull.Value : imageUrl.Trim());

        cmd.ExecuteNonQuery();
        return (true, "Đã gửi đánh giá của bạn.");
    }

    public (bool Success, string Message) ToggleBookReviewLike(int reviewId, int userId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            using (var checkReviewCmd = new SqlCommand("SELECT TOP 1 1 FROM dbo.book_reviews WHERE id = @reviewId;", connection, tx))
            {
                checkReviewCmd.Parameters.AddWithValue("@reviewId", reviewId);
                if (checkReviewCmd.ExecuteScalar() is null)
                {
                    tx.Rollback();
                    return (false, "Không tìm thấy bình luận.");
                }
            }

            using var existsCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.book_review_likes
WHERE review_id = @reviewId AND user_id = @userId;", connection, tx);

            existsCmd.Parameters.AddWithValue("@reviewId", reviewId);
            existsCmd.Parameters.AddWithValue("@userId", userId);
            var exists = existsCmd.ExecuteScalar() is not null;

            if (exists)
            {
                using var deleteCmd = new SqlCommand(@"
DELETE FROM dbo.book_review_likes
WHERE review_id = @reviewId AND user_id = @userId;", connection, tx);
                deleteCmd.Parameters.AddWithValue("@reviewId", reviewId);
                deleteCmd.Parameters.AddWithValue("@userId", userId);
                deleteCmd.ExecuteNonQuery();
            }
            else
            {
                using var insertCmd = new SqlCommand(@"
INSERT INTO dbo.book_review_likes (review_id, user_id)
VALUES (@reviewId, @userId);", connection, tx);
                insertCmd.Parameters.AddWithValue("@reviewId", reviewId);
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return (true, exists ? "Đã bỏ tim bình luận." : "Đã tim bình luận.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public (bool Success, string Message) ReplyToBookReview(int reviewId, int adminUserId, string replyContent)
    {
        var normalizedReply = (replyContent ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedReply))
        {
            return (false, "Nội dung phản hồi không được để trống.");
        }

        using var connection = CreateConnection();
        connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            using (var checkAdminCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.user_accounts
WHERE id = @adminId AND role = N'Admin';", connection, tx))
            {
                checkAdminCmd.Parameters.AddWithValue("@adminId", adminUserId);
                if (checkAdminCmd.ExecuteScalar() is null)
                {
                    tx.Rollback();
                    return (false, "Bạn không có quyền phản hồi bình luận.");
                }
            }

            using (var checkReviewCmd = new SqlCommand("SELECT TOP 1 1 FROM dbo.book_reviews WHERE id = @reviewId;", connection, tx))
            {
                checkReviewCmd.Parameters.AddWithValue("@reviewId", reviewId);
                if (checkReviewCmd.ExecuteScalar() is null)
                {
                    tx.Rollback();
                    return (false, "Không tìm thấy bình luận cần phản hồi.");
                }
            }

            using var upsertCmd = new SqlCommand(@"
MERGE dbo.book_review_replies AS target
USING (SELECT @reviewId AS review_id) AS source
ON target.review_id = source.review_id
WHEN MATCHED THEN
    UPDATE SET
        reply_content = @replyContent,
        admin_user_id = @adminUserId,
        replied_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (review_id, admin_user_id, reply_content)
    VALUES (@reviewId, @adminUserId, @replyContent);", connection, tx);

            upsertCmd.Parameters.AddWithValue("@reviewId", reviewId);
            upsertCmd.Parameters.AddWithValue("@adminUserId", adminUserId);
            upsertCmd.Parameters.AddWithValue("@replyContent", normalizedReply);
            upsertCmd.ExecuteNonQuery();

            tx.Commit();
            return (true, "Đã phản hồi bình luận.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public Book AddBook(AdminBookInputModel input)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
INSERT INTO books (title, author, category_id, price, stock, description, cover_url)
OUTPUT INSERTED.id, INSERTED.title, INSERTED.author, INSERTED.price, INSERTED.stock, INSERTED.category_id, INSERTED.description, INSERTED.cover_url
VALUES (@title, @author, @categoryId, @price, @stock, @description, @coverUrl);", connection);

        cmd.Parameters.AddWithValue("@title", input.Title.Trim());
        cmd.Parameters.AddWithValue("@author", input.Author.Trim());
        cmd.Parameters.AddWithValue("@categoryId", input.CategoryId);
        cmd.Parameters.AddWithValue("@price", input.Price);
        cmd.Parameters.AddWithValue("@stock", input.Stock);
        cmd.Parameters.AddWithValue("@description", input.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@coverUrl", input.CoverUrl ?? string.Empty);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Khong the them sach.");
        }

        return MapBook(reader);
    }

    public bool UpdateBook(int id, AdminBookInputModel input)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
UPDATE books
SET title = @title,
    author = @author,
    category_id = @categoryId,
    price = @price,
    stock = @stock,
    description = @description,
    cover_url = @coverUrl
WHERE id = @id;", connection);

        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@title", input.Title.Trim());
        cmd.Parameters.AddWithValue("@author", input.Author.Trim());
        cmd.Parameters.AddWithValue("@categoryId", input.CategoryId);
        cmd.Parameters.AddWithValue("@price", input.Price);
        cmd.Parameters.AddWithValue("@stock", input.Stock);
        cmd.Parameters.AddWithValue("@description", input.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@coverUrl", input.CoverUrl ?? string.Empty);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteBook(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using (var checkCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM order_items
WHERE book_id = @id;", connection))
        {
            checkCmd.Parameters.AddWithValue("@id", id);
            if (checkCmd.ExecuteScalar() is not null)
            {
                return false;
            }
        }

        using var deleteCmd = new SqlCommand(@"
DELETE FROM books
WHERE id = @id;", connection);

        deleteCmd.Parameters.AddWithValue("@id", id);
        return deleteCmd.ExecuteNonQuery() > 0;
    }

    public Category AddCategory(AdminCategoryInputModel input)
    {
        var normalized = input.Name.Trim();

        using var connection = CreateConnection();
        connection.Open();

        using (var checkCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM categories
WHERE LOWER(name) = LOWER(@name);", connection))
        {
            checkCmd.Parameters.AddWithValue("@name", normalized);
            if (checkCmd.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException("Danh muc da ton tai.");
            }
        }

        using var insertCmd = new SqlCommand(@"
INSERT INTO categories (name)
OUTPUT INSERTED.id, INSERTED.name
VALUES (@name);", connection);

        insertCmd.Parameters.AddWithValue("@name", normalized);

        using var reader = insertCmd.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Khong the tao danh muc.");
        }

        return new Category
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1)
        };
    }

    public bool UpdateCategory(int id, AdminCategoryInputModel input)
    {
        var normalized = input.Name.Trim();

        using var connection = CreateConnection();
        connection.Open();

        using (var checkCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM categories
WHERE id <> @id
  AND LOWER(name) = LOWER(@name);", connection))
        {
            checkCmd.Parameters.AddWithValue("@id", id);
            checkCmd.Parameters.AddWithValue("@name", normalized);
            if (checkCmd.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException("Danh muc da ton tai.");
            }
        }

        using var updateCmd = new SqlCommand(@"
UPDATE categories
SET name = @name
WHERE id = @id;", connection);

        updateCmd.Parameters.AddWithValue("@id", id);
        updateCmd.Parameters.AddWithValue("@name", normalized);

        return updateCmd.ExecuteNonQuery() > 0;
    }

    public (bool Success, string Message) DeleteCategory(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using (var existsCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM categories
WHERE id = @id;", connection))
        {
            existsCmd.Parameters.AddWithValue("@id", id);
            if (existsCmd.ExecuteScalar() is null)
            {
                return (false, "Khong tim thay danh muc.");
            }
        }

        using (var hasBooksCmd = new SqlCommand(@"
SELECT TOP 1 1
FROM books
WHERE category_id = @id;", connection))
        {
            hasBooksCmd.Parameters.AddWithValue("@id", id);
            if (hasBooksCmd.ExecuteScalar() is not null)
            {
                return (false, "Danh muc dang co sach, khong the xoa.");
            }
        }

        using var deleteCmd = new SqlCommand(@"
DELETE FROM categories
WHERE id = @id;", connection);

        deleteCmd.Parameters.AddWithValue("@id", id);
        var affected = deleteCmd.ExecuteNonQuery();

        return affected > 0
            ? (true, "Da xoa danh muc.")
            : (false, "Khong tim thay danh muc.");
    }

    public IReadOnlyList<Order> GetOrders()
    {
        var orders = new List<Order>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT id, user_id, customer_name, email, phone, address, payment_method, payment_status, paid_at, payment_note, total_amount, created_at
FROM orders
ORDER BY created_at DESC;", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                UserId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                CustomerName = reader.GetString(2),
                Email = reader.GetString(3),
                Phone = reader.GetString(4),
                Address = reader.GetString(5),
                PaymentMethod = reader.IsDBNull(6) ? PaymentMethods.CashOnDelivery : reader.GetString(6),
                PaymentStatus = reader.IsDBNull(7) ? "Pending" : reader.GetString(7),
                PaidAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                PaymentNote = reader.IsDBNull(9) ? null : reader.GetString(9),
                TotalAmount = reader.GetDecimal(10),
                CreatedAt = reader.GetDateTime(11),
                Items = []
            });
        }

        return orders;
    }

    public Order? GetOrder(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        Order? order;

        using (var orderCmd = new SqlCommand(@"
SELECT TOP 1 id, user_id, customer_name, email, phone, address, payment_method, payment_status, paid_at, payment_note, total_amount, created_at
FROM orders
WHERE id = @id;", connection))
        {
            orderCmd.Parameters.AddWithValue("@id", id);

            using var orderReader = orderCmd.ExecuteReader();
            if (!orderReader.Read())
            {
                return null;
            }

            order = new Order
            {
                Id = orderReader.GetInt32(0),
                UserId = orderReader.IsDBNull(1) ? null : orderReader.GetInt32(1),
                CustomerName = orderReader.GetString(2),
                Email = orderReader.GetString(3),
                Phone = orderReader.GetString(4),
                Address = orderReader.GetString(5),
                PaymentMethod = orderReader.IsDBNull(6) ? PaymentMethods.CashOnDelivery : orderReader.GetString(6),
                PaymentStatus = orderReader.IsDBNull(7) ? "Pending" : orderReader.GetString(7),
                PaidAt = orderReader.IsDBNull(8) ? null : orderReader.GetDateTime(8),
                PaymentNote = orderReader.IsDBNull(9) ? null : orderReader.GetString(9),
                TotalAmount = orderReader.GetDecimal(10),
                CreatedAt = orderReader.GetDateTime(11),
                Items = []
            };
        }

        using var itemCmd = new SqlCommand(@"
SELECT book_id, book_title, unit_price, quantity
FROM order_items
WHERE order_id = @orderId
ORDER BY id;", connection);

        itemCmd.Parameters.AddWithValue("@orderId", id);

        using var itemReader = itemCmd.ExecuteReader();
        while (itemReader.Read())
        {
            order.Items.Add(new OrderItem
            {
                BookId = itemReader.GetInt32(0),
                BookTitle = itemReader.GetString(1),
                UnitPrice = itemReader.GetDecimal(2),
                Quantity = itemReader.GetInt32(3)
            });
        }

        return order;
    }

    public int CreateOrder(CheckoutInputModel input, IReadOnlyList<CartItemViewModel> items, int? userId = null, string? paymentNote = null)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var line in items)
            {
                using var updateStockCmd = new SqlCommand(@"
UPDATE books
SET stock = stock - @quantity
WHERE id = @bookId
  AND stock >= @quantity;", connection, transaction);

                updateStockCmd.Parameters.AddWithValue("@bookId", line.BookId);
                updateStockCmd.Parameters.AddWithValue("@quantity", line.Quantity);

                var affectedRows = updateStockCmd.ExecuteNonQuery();
                if (affectedRows == 0)
                {
                    throw new InvalidOperationException($"Sach '{line.Title}' da het hoac khong du so luong ton.");
                }
            }

            using var orderCmd = new SqlCommand(@"
INSERT INTO orders (user_id, customer_name, email, phone, address, payment_method, payment_status, payment_note, total_amount)
OUTPUT INSERTED.id
VALUES (@userId, @customerName, @email, @phone, @address, @paymentMethod, @paymentStatus, @paymentNote, @totalAmount);", connection, transaction);

            var userIdParam = orderCmd.Parameters.Add("@userId", SqlDbType.Int);
            userIdParam.Value = userId.HasValue ? userId.Value : DBNull.Value;
            orderCmd.Parameters.AddWithValue("@customerName", input.CustomerName.Trim());
            orderCmd.Parameters.AddWithValue("@email", input.Email.Trim());
            orderCmd.Parameters.AddWithValue("@phone", input.Phone.Trim());
            orderCmd.Parameters.AddWithValue("@address", input.Address.Trim());
            var paymentMethod = string.IsNullOrWhiteSpace(input.PaymentMethod) ? PaymentMethods.CashOnDelivery : input.PaymentMethod.Trim().ToUpperInvariant();
            var paymentStatus = string.Equals(paymentMethod, PaymentMethods.VietQr, StringComparison.OrdinalIgnoreCase)
                ? "PendingPayment"
                : "Pending";
            orderCmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
            orderCmd.Parameters.AddWithValue("@paymentStatus", paymentStatus);
            orderCmd.Parameters.AddWithValue("@paymentNote", string.IsNullOrWhiteSpace(paymentNote) ? DBNull.Value : paymentNote.Trim());
            orderCmd.Parameters.AddWithValue("@totalAmount", items.Sum(x => x.LineTotal));

            var orderIdObj = orderCmd.ExecuteScalar();
            if (orderIdObj is null)
            {
                throw new InvalidOperationException("Khong the tao don hang.");
            }

            var orderId = Convert.ToInt32(orderIdObj);

            foreach (var line in items)
            {
                using var itemCmd = new SqlCommand(@"
INSERT INTO order_items (order_id, book_id, book_title, unit_price, quantity)
VALUES (@orderId, @bookId, @bookTitle, @unitPrice, @quantity);", connection, transaction);

                itemCmd.Parameters.AddWithValue("@orderId", orderId);
                itemCmd.Parameters.AddWithValue("@bookId", line.BookId);
                itemCmd.Parameters.AddWithValue("@bookTitle", line.Title);
                itemCmd.Parameters.AddWithValue("@unitPrice", line.Price);
                itemCmd.Parameters.AddWithValue("@quantity", line.Quantity);

                itemCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return orderId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IReadOnlyList<CartItemViewModel> GetCartItems(int userId)
    {
        var items = new List<CartItemViewModel>();

        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand(@"
SELECT 
    c.book_id,
    b.title,
    b.cover_url,
    b.price,
    c.quantity
FROM dbo.cart_items c
INNER JOIN dbo.books b ON c.book_id = b.id
WHERE c.user_id = @userId
ORDER BY c.created_at ASC;", connection);

        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new CartItemViewModel
            {
                BookId = reader.GetInt32(0),
                Title = reader.GetString(1),
                CoverUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Price = reader.GetDecimal(3),
                Quantity = reader.GetInt32(4)
            });
        }

        return items;
    }

    public void SyncCartItems(int userId, IEnumerable<CartItemViewModel> items)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            using (var deleteCmd = new SqlCommand("DELETE FROM dbo.cart_items WHERE user_id = @userId;", connection, tx))
            {
                deleteCmd.Parameters.AddWithValue("@userId", userId);
                deleteCmd.ExecuteNonQuery();
            }

            if (items != null && items.Any())
            {
                var insertSql = @"
INSERT INTO dbo.cart_items (user_id, book_id, quantity, created_at, updated_at) 
VALUES (@userId, @bookId, @quantity, SYSUTCDATETIME(), SYSUTCDATETIME());";

                foreach (var item in items)
                {
                    using var insertCmd = new SqlCommand(insertSql, connection, tx);
                    insertCmd.Parameters.AddWithValue("@userId", userId);
                    insertCmd.Parameters.AddWithValue("@bookId", item.BookId);
                    insertCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                    insertCmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void ClearCart(int userId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var cmd = new SqlCommand("DELETE FROM dbo.cart_items WHERE user_id = @userId;", connection);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }

    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    private static UserAccount MapUser(SqlDataReader reader)
    {
        return new UserAccount
        {
            Id = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            PasswordSalt = reader.GetString(4),
            Role = reader.GetString(5),
            CreatedAt = reader.GetDateTime(6)
        };
    }

    private static Book MapBook(SqlDataReader reader)
    {
        return new Book
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Author = reader.GetString(2),
            Price = reader.GetDecimal(3),
            Stock = reader.GetInt32(4),
            CategoryId = reader.GetInt32(5),
            Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            CoverUrl = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
        };
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
}
