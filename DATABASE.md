﻿# DATABASE.md - BookStore (SQL Server)

Tai lieu nay la ban SQL Server de ban copy/chay truc tiep trong SSMS.

## 1) Script tao database + bang + index + du lieu mau

> Yeu cau: SQL Server 2019+ (hoac 2017 van chay duoc phan lon script)

```sql
/* =====================================================
   BookStore SQL Server Full Schema
   ===================================================== */

IF DB_ID(N'book_store_db') IS NULL
BEGIN
    CREATE DATABASE [book_store_db];
END
GO

USE [book_store_db];
GO

/* -----------------------------------------------------
   1. USER ACCOUNTS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.user_accounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.user_accounts
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        full_name NVARCHAR(120) NOT NULL,
        email NVARCHAR(150) NOT NULL,
        password_hash NVARCHAR(255) NOT NULL,
        password_salt NVARCHAR(255) NOT NULL,
        role NVARCHAR(20) NOT NULL CONSTRAINT DF_user_accounts_role DEFAULT N'User',
        created_at DATETIME2 NOT NULL CONSTRAINT DF_user_accounts_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_user_accounts_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_user_accounts_email UNIQUE (email),
        CONSTRAINT CHK_user_accounts_role CHECK (role IN (N'Admin', N'User'))
    );
END
GO

/* -----------------------------------------------------
   2. CATEGORIES
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.categories
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        name NVARCHAR(100) NOT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_categories_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_categories_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_categories_name UNIQUE (name)
    );
END
GO

/* -----------------------------------------------------
   3. BOOKS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.books', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.books
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        title NVARCHAR(255) NOT NULL,
        author NVARCHAR(150) NOT NULL,
        category_id INT NOT NULL,
        price DECIMAL(12,2) NOT NULL,
        stock INT NOT NULL CONSTRAINT DF_books_stock DEFAULT 0,
        description NVARCHAR(MAX) NULL,
        cover_url NVARCHAR(700) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_books_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_books_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_books_categories FOREIGN KEY (category_id) REFERENCES dbo.categories(id),
        CONSTRAINT CHK_books_price CHECK (price >= 0),
        CONSTRAINT CHK_books_stock CHECK (stock >= 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_books_category_id' AND object_id = OBJECT_ID(N'dbo.books'))
    CREATE INDEX IX_books_category_id ON dbo.books(category_id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_books_title' AND object_id = OBJECT_ID(N'dbo.books'))
    CREATE INDEX IX_books_title ON dbo.books(title);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_books_author' AND object_id = OBJECT_ID(N'dbo.books'))
    CREATE INDEX IX_books_author ON dbo.books(author);
GO

/* -----------------------------------------------------
   4. ORDERS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.orders
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id INT NULL,
        customer_name NVARCHAR(150) NOT NULL,
        email NVARCHAR(150) NOT NULL,
        phone NVARCHAR(30) NOT NULL,
        address NVARCHAR(400) NOT NULL,
        payment_method NVARCHAR(20) NOT NULL CONSTRAINT DF_orders_payment_method DEFAULT N'COD',
        payment_status NVARCHAR(30) NOT NULL CONSTRAINT DF_orders_payment_status DEFAULT N'Pending',
        paid_at DATETIME2 NULL,
        payment_note NVARCHAR(500) NULL,
        total_amount DECIMAL(12,2) NOT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_orders_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_orders_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_orders_users FOREIGN KEY (user_id) REFERENCES dbo.user_accounts(id),
        CONSTRAINT CHK_orders_payment_method CHECK (payment_method IN (N'COD', N'VIETQR')),
        CONSTRAINT CHK_orders_total_amount CHECK (total_amount >= 0)
    );
END
GO

/* Ho tro cap nhat schema cho DB da ton tai (idempotent) */
IF COL_LENGTH(N'dbo.orders', N'payment_method') IS NULL
    ALTER TABLE dbo.orders ADD payment_method NVARCHAR(20) NOT NULL CONSTRAINT DF_orders_payment_method_legacy DEFAULT N'COD' WITH VALUES;
GO
IF COL_LENGTH(N'dbo.orders', N'payment_status') IS NULL
    ALTER TABLE dbo.orders ADD payment_status NVARCHAR(30) NOT NULL CONSTRAINT DF_orders_payment_status_legacy DEFAULT N'Pending' WITH VALUES;
GO
IF COL_LENGTH(N'dbo.orders', N'paid_at') IS NULL
    ALTER TABLE dbo.orders ADD paid_at DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.orders', N'payment_note') IS NULL
    ALTER TABLE dbo.orders ADD payment_note NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_orders_payment_method')
    ALTER TABLE dbo.orders ADD CONSTRAINT CHK_orders_payment_method CHECK (payment_method IN (N'COD', N'VIETQR'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_orders_user_id' AND object_id = OBJECT_ID(N'dbo.orders'))
    CREATE INDEX IX_orders_user_id ON dbo.orders(user_id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_orders_created_at' AND object_id = OBJECT_ID(N'dbo.orders'))
    CREATE INDEX IX_orders_created_at ON dbo.orders(created_at);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_orders_payment_status' AND object_id = OBJECT_ID(N'dbo.orders'))
    CREATE INDEX IX_orders_payment_status ON dbo.orders(payment_status);
GO

/* -----------------------------------------------------
   5. ORDER ITEMS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.order_items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.order_items
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        order_id INT NOT NULL,
        book_id INT NOT NULL,
        book_title NVARCHAR(255) NOT NULL,
        unit_price DECIMAL(12,2) NOT NULL,
        quantity INT NOT NULL,
        line_total AS (unit_price * quantity) PERSISTED,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_order_items_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_order_items_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_order_items_orders FOREIGN KEY (order_id) REFERENCES dbo.orders(id) ON DELETE CASCADE,
        CONSTRAINT FK_order_items_books FOREIGN KEY (book_id) REFERENCES dbo.books(id),
        CONSTRAINT CHK_order_items_unit_price CHECK (unit_price >= 0),
        CONSTRAINT CHK_order_items_quantity CHECK (quantity > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_order_items_order_id' AND object_id = OBJECT_ID(N'dbo.order_items'))
    CREATE INDEX IX_order_items_order_id ON dbo.order_items(order_id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_order_items_book_id' AND object_id = OBJECT_ID(N'dbo.order_items'))
    CREATE INDEX IX_order_items_book_id ON dbo.order_items(book_id);
GO

/* -----------------------------------------------------
   6. BOOK REVIEWS / COMMENTS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.book_reviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.book_reviews
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        book_id INT NOT NULL,
        user_id INT NOT NULL,
        rating INT NOT NULL,
        comment NVARCHAR(1500) NOT NULL,
        image_url NVARCHAR(700) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_book_reviews_created_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_book_reviews_books FOREIGN KEY (book_id) REFERENCES dbo.books(id) ON DELETE CASCADE,
        CONSTRAINT FK_book_reviews_users FOREIGN KEY (user_id) REFERENCES dbo.user_accounts(id),
        CONSTRAINT CHK_book_reviews_rating CHECK (rating BETWEEN 1 AND 5)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_book_reviews_book_id' AND object_id = OBJECT_ID(N'dbo.book_reviews'))
    CREATE INDEX IX_book_reviews_book_id ON dbo.book_reviews(book_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_book_reviews_created_at' AND object_id = OBJECT_ID(N'dbo.book_reviews'))
    CREATE INDEX IX_book_reviews_created_at ON dbo.book_reviews(created_at DESC);
GO

IF OBJECT_ID(N'dbo.book_review_likes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.book_review_likes
    (
        review_id INT NOT NULL,
        user_id INT NOT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_book_review_likes_created_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_book_review_likes PRIMARY KEY (review_id, user_id),
        CONSTRAINT FK_book_review_likes_reviews FOREIGN KEY (review_id) REFERENCES dbo.book_reviews(id) ON DELETE CASCADE,
        CONSTRAINT FK_book_review_likes_users FOREIGN KEY (user_id) REFERENCES dbo.user_accounts(id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_book_review_likes_user_id' AND object_id = OBJECT_ID(N'dbo.book_review_likes'))
    CREATE INDEX IX_book_review_likes_user_id ON dbo.book_review_likes(user_id);
GO

IF OBJECT_ID(N'dbo.book_review_replies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.book_review_replies
    (
        review_id INT NOT NULL PRIMARY KEY,
        admin_user_id INT NOT NULL,
        reply_content NVARCHAR(1500) NOT NULL,
        replied_at DATETIME2 NOT NULL CONSTRAINT DF_book_review_replies_replied_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_book_review_replies_reviews FOREIGN KEY (review_id) REFERENCES dbo.book_reviews(id) ON DELETE CASCADE,
        CONSTRAINT FK_book_review_replies_admin_users FOREIGN KEY (admin_user_id) REFERENCES dbo.user_accounts(id)
    );
END
GO

/* -----------------------------------------------------
   6.5 CART ITEMS
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.cart_items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.cart_items
    (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id INT NOT NULL,
        book_id INT NOT NULL,
        quantity INT NOT NULL CONSTRAINT DF_cart_items_quantity DEFAULT 1,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_cart_items_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_cart_items_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_cart_items_users FOREIGN KEY (user_id) REFERENCES dbo.user_accounts(id) ON DELETE CASCADE,
        CONSTRAINT FK_cart_items_books FOREIGN KEY (book_id) REFERENCES dbo.books(id) ON DELETE CASCADE,
        CONSTRAINT UQ_cart_items_user_book UNIQUE (user_id, book_id),
        CONSTRAINT CHK_cart_items_quantity CHECK (quantity > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_cart_items_user_id' AND object_id = OBJECT_ID(N'dbo.cart_items'))
    CREATE INDEX IX_cart_items_user_id ON dbo.cart_items(user_id);
GO

/* -----------------------------------------------------
   7. TRIGGER cap nhat updated_at
   ----------------------------------------------------- */
IF OBJECT_ID(N'dbo.trg_user_accounts_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_user_accounts_updated_at ON dbo.user_accounts AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE ua SET updated_at = SYSUTCDATETIME() FROM dbo.user_accounts ua INNER JOIN inserted i ON ua.id = i.id; END');
GO

IF OBJECT_ID(N'dbo.trg_categories_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_categories_updated_at ON dbo.categories AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE c SET updated_at = SYSUTCDATETIME() FROM dbo.categories c INNER JOIN inserted i ON c.id = i.id; END');
GO

IF OBJECT_ID(N'dbo.trg_books_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_books_updated_at ON dbo.books AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE b SET updated_at = SYSUTCDATETIME() FROM dbo.books b INNER JOIN inserted i ON b.id = i.id; END');
GO

IF OBJECT_ID(N'dbo.trg_orders_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_orders_updated_at ON dbo.orders AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE o SET updated_at = SYSUTCDATETIME() FROM dbo.orders o INNER JOIN inserted i ON o.id = i.id; END');
GO

IF OBJECT_ID(N'dbo.trg_order_items_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_order_items_updated_at ON dbo.order_items AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE oi SET updated_at = SYSUTCDATETIME() FROM dbo.order_items oi INNER JOIN inserted i ON oi.id = i.id; END');
GO

IF OBJECT_ID(N'dbo.trg_cart_items_updated_at', N'TR') IS NULL
EXEC('CREATE TRIGGER dbo.trg_cart_items_updated_at ON dbo.cart_items AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE c SET updated_at = SYSUTCDATETIME() FROM dbo.cart_items c INNER JOIN inserted i ON c.id = i.id; END');
GO

/* -----------------------------------------------------
   8. SEED CATEGORIES (idempotent)
   ----------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.categories WHERE name = N'Lap trinh')
    INSERT INTO dbo.categories(name) VALUES (N'Lap trinh');

IF NOT EXISTS (SELECT 1 FROM dbo.categories WHERE name = N'Kinh te')
    INSERT INTO dbo.categories(name) VALUES (N'Kinh te');

IF NOT EXISTS (SELECT 1 FROM dbo.categories WHERE name = N'Phat trien ban than')
    INSERT INTO dbo.categories(name) VALUES (N'Phat trien ban than');

IF NOT EXISTS (SELECT 1 FROM dbo.categories WHERE name = N'Van hoc')
    INSERT INTO dbo.categories(name) VALUES (N'Van hoc');
GO

/* -----------------------------------------------------
   9. SEED BOOKS (idempotent)
   ----------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.books WHERE title = N'Clean Code' AND author = N'Robert C. Martin')
BEGIN
    INSERT INTO dbo.books (title, author, category_id, price, stock, description, cover_url)
    VALUES
    (
        N'Clean Code',
        N'Robert C. Martin',
        (SELECT TOP 1 id FROM dbo.categories WHERE name = N'Lap trinh'),
        259000,
        20,
        N'Sach kinh dien ve ky thuat viet code de doc, de bao tri.',
        N'https://images.unsplash.com/photo-1512820790803-83ca734da794?w=900'
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.books WHERE title = N'The Pragmatic Programmer' AND author = N'Andrew Hunt')
BEGIN
    INSERT INTO dbo.books (title, author, category_id, price, stock, description, cover_url)
    VALUES
    (
        N'The Pragmatic Programmer',
        N'Andrew Hunt',
        (SELECT TOP 1 id FROM dbo.categories WHERE name = N'Lap trinh'),
        299000,
        15,
        N'Huong dan tu duy va ky nang lap trinh thuc te.',
        N'https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=900'
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.books WHERE title = N'Nha Gia Kim' AND author = N'Paulo Coelho')
BEGIN
    INSERT INTO dbo.books (title, author, category_id, price, stock, description, cover_url)
    VALUES
    (
        N'Nha Gia Kim',
        N'Paulo Coelho',
        (SELECT TOP 1 id FROM dbo.categories WHERE name = N'Van hoc'),
        99000,
        30,
        N'Tieu thuyet truyen cam hung noi tieng tren toan the gioi.',
        N'https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=900'
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.books WHERE title = N'Atomic Habits' AND author = N'James Clear')
BEGIN
    INSERT INTO dbo.books (title, author, category_id, price, stock, description, cover_url)
    VALUES
    (
        N'Atomic Habits',
        N'James Clear',
        (SELECT TOP 1 id FROM dbo.categories WHERE name = N'Phat trien ban than'),
        189000,
        18,
        N'Phuong phap xay dung thoi quen tot va loai bo thoi quen xau.',
        N'https://images.unsplash.com/photo-1516979187457-637abb4f9353?w=900'
    );
END
GO

/* -----------------------------------------------------
   10. OPTIONAL: SEED ADMIN ACCOUNT
   ----------------------------------------------------- */
-- Chu y: App hien hash mat khau theo PBKDF2 SHA256 + salt (Base64).
-- Ban can tu tao hash/salt roi insert vao day neu muon login bang DB that.

-- IF NOT EXISTS (SELECT 1 FROM dbo.user_accounts WHERE email = N'admin@bookstore.local')
-- BEGIN
--     INSERT INTO dbo.user_accounts (full_name, email, password_hash, password_salt, role)
--     VALUES (N'System Admin', N'admin@bookstore.local', N'BASE64_HASH', N'BASE64_SALT', N'Admin');
-- END
-- GO
```

## 2) Cau hinh connection string SQL Server trong appsettings.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=book_store_db;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;Encrypt=False;"
  }
}
```

Neu dung Windows Authentication:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=book_store_db;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
  }
}
```

## 3) Kiem tra nhanh sau khi tao DB

```sql
USE [book_store_db];

SELECT COUNT(*) AS total_categories FROM dbo.categories;
SELECT COUNT(*) AS total_books FROM dbo.books;
SELECT COUNT(*) AS total_users FROM dbo.user_accounts;
SELECT COUNT(*) AS total_orders FROM dbo.orders;
SELECT COUNT(*) AS total_reviews FROM dbo.book_reviews;
SELECT COUNT(*) AS total_review_likes FROM dbo.book_review_likes;

SELECT b.id, b.title, b.author, c.name AS category_name, b.price, b.stock
FROM dbo.books b
JOIN dbo.categories c ON c.id = b.category_id
ORDER BY b.id;

SELECT TOP 20 r.id, b.title, u.full_name, r.rating, r.comment, r.created_at
FROM dbo.book_reviews r
JOIN dbo.books b ON b.id = r.book_id
JOIN dbo.user_accounts u ON u.id = r.user_id
ORDER BY r.created_at DESC;
```

## 4) Mapping bang <-> model hien tai

- `user_accounts` <-> `UserAccount`
- `categories` <-> `Category`
- `books` <-> `Book`
- `orders` <-> `Order` (co `payment_method`, `payment_status`, `paid_at`, `payment_note`)
- `order_items` <-> `OrderItem`
- `cart_items` <-> `CartItemViewModel` (luu tru trong DB thay vi Session)
- `book_reviews` <-> `BookReviewViewModel`
- `book_review_likes` <-> tim binh luan
- `book_review_replies` <-> phan hoi cua admin
- Dieu kien nghiep vu: chi user da mua sach (co `orders.user_id` va `order_items.book_id` tuong ung) moi duoc phep them `book_reviews`.

## 5) Luu y ve code hien tai

- App dang duoc cau hinh su dung `SqlServerStoreService`.
- Can chay script trong file nay de tao day du bang du lieu (bao gom bang binh luan/like/phan hoi) truoc khi khoi dong app.
- Neu ban muon dung ban RAM de demo nhanh, co the doi lai DI sang `InMemoryStoreService` trong `Program.cs`.

## 6) Xoa du lieu de seed lai (tuy chon)

```sql
USE [book_store_db];

DELETE FROM dbo.order_items;
DELETE FROM dbo.cart_items;
DELETE FROM dbo.orders;
DELETE FROM dbo.book_review_likes;
DELETE FROM dbo.book_review_replies;
DELETE FROM dbo.book_reviews;
DELETE FROM dbo.books;
DELETE FROM dbo.categories;
DELETE FROM dbo.user_accounts;

DBCC CHECKIDENT ('dbo.order_items', RESEED, 0);
DBCC CHECKIDENT ('dbo.cart_items', RESEED, 0);
DBCC CHECKIDENT ('dbo.orders', RESEED, 0);
DBCC CHECKIDENT ('dbo.book_reviews', RESEED, 0);
DBCC CHECKIDENT ('dbo.books', RESEED, 0);
DBCC CHECKIDENT ('dbo.categories', RESEED, 0);
DBCC CHECKIDENT ('dbo.user_accounts', RESEED, 0);
```
