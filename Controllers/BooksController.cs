using BookStore.Models;
using BookStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Controllers;

public class BooksController : Controller
{
    private readonly IStoreService _storeService;
    private readonly IWebHostEnvironment _environment;

    public BooksController(IStoreService storeService, IWebHostEnvironment environment)
    {
        _storeService = storeService;
        _environment = environment;
    }

    public IActionResult Index(int? categoryId, string? keyword)
    {
        var model = new BooksIndexViewModel
        {
            Books = _storeService.GetBooks(categoryId, keyword),
            Categories = _storeService.GetCategories(),
            SelectedCategoryId = categoryId,
            Keyword = keyword ?? string.Empty
        };

        return View(model);
    }

    public IActionResult Products(int? categoryId, string? keyword, string? sort, int? minPrice, int? maxPrice, int page = 1)
    {
        var normalizedSort = (sort ?? "newest").Trim().ToLowerInvariant();
        var allowedSorts = new[] { "newest", "price", "relevance" };
        if (!allowedSorts.Contains(normalizedSort))
        {
            normalizedSort = "newest";
        }

        const int pageSize = 6;
        if (page < 1)
        {
            page = 1;
        }

        var books = _storeService.GetBooks(categoryId, keyword);
        if (minPrice.HasValue)
        {
            books = books.Where(x => x.Price >= minPrice.Value).ToList();
        }

        if (maxPrice.HasValue)
        {
            books = books.Where(x => x.Price <= maxPrice.Value).ToList();
        }

        books = normalizedSort switch
        {
            "price" => books.OrderBy(x => x.Price).ThenBy(x => x.Title).ToList(),
            "relevance" => books.OrderBy(x => x.Title).ThenBy(x => x.Author).ToList(),
            _ => books.OrderByDescending(x => x.Id).ToList()
        };

        var totalItems = books.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var pagedBooks = books
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var model = new BooksIndexViewModel
        {
            Books = pagedBooks,
            Categories = _storeService.GetCategories(),
            SelectedCategoryId = categoryId,
            Keyword = keyword ?? string.Empty
        };

        ViewBag.Sort = normalizedSort;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageSize = pageSize;
        return View(model);
    }

    public IActionResult Details(int id)
    {
        var book = _storeService.GetBook(id);
        if (book is null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        var canReview = currentUserId.HasValue && _storeService.HasUserPurchasedBook(currentUserId.Value, id);
        var model = new BookDetailsViewModel
        {
            Book = book,
            Reviews = _storeService.GetBookReviews(id, currentUserId),
            CanReview = canReview
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddReview(int id, AddReviewInputModel input)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Vui lòng đăng nhập để gửi đánh giá.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu đánh giá chưa hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!_storeService.HasUserPurchasedBook(userId.Value, id))
        {
            TempData["ErrorMessage"] = "Bạn cần mua sách này trước khi gửi đánh giá.";
            return RedirectToAction(nameof(Details), new { id });
        }

        string? imageUrl = null;
        if (input.ImageFile is not null && input.ImageFile.Length > 0)
        {
            var saveResult = SaveReviewImage(input.ImageFile);
            if (!saveResult.Success)
            {
                TempData["ErrorMessage"] = saveResult.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            imageUrl = saveResult.ImageUrl;
        }

        var result = _storeService.AddBookReview(id, userId.Value, input.Rating, input.Comment, imageUrl);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleReviewLike(int id, int reviewId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Vui lòng đăng nhập để tim bình luận.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = _storeService.ToggleBookReviewLike(reviewId, userId.Value);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReplyReview(int id, int reviewId, string replyContent)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
        {
            TempData["ErrorMessage"] = "Bạn chưa đăng nhập.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = _storeService.ReplyToBookReview(reviewId, adminUserId.Value, replyContent);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult Suggest(string? keyword)
    {
        var term = (keyword ?? string.Empty).Trim();
        if (term.Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        var suggestions = _storeService.GetBooks(keyword: term)
            .Take(6)
            .Select(book => new
            {
                book.Id,
                book.Title,
                book.Author,
                book.CoverUrl,
                DetailsUrl = Url.Action(nameof(Details), "Books", new { id = book.Id }) ?? $"/Books/Details/{book.Id}"
            });

        return Json(suggestions);
    }

    private int? GetCurrentUserId()
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdRaw, out var userId) ? userId : null;
    }

    private (bool Success, string Message, string? ImageUrl) SaveReviewImage(IFormFile file)
    {
        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return (false, "Ảnh quá lớn, vui lòng chọn ảnh dưới 5MB.", null);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowed.Contains(extension))
        {
            return (false, "Định dạng ảnh không hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP hoặc GIF.", null);
        }

        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return (false, "Không thể xác định thư mục lưu ảnh.", null);
        }

        var uploadDir = Path.Combine(webRootPath, "uploads", "reviews");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var savePath = Path.Combine(uploadDir, fileName);
        using (var stream = System.IO.File.Create(savePath))
        {
            file.CopyTo(stream);
        }

        return (true, string.Empty, $"/uploads/reviews/{fileName}");
    }
}
