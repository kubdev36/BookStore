using BookStore.Models;
using BookStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public class AdminController : Controller
{
    private readonly IStoreService _storeService;

    public AdminController(IStoreService storeService)
    {
        _storeService = storeService;
    }

    public IActionResult Dashboard()
    {
        var books = _storeService.GetBooks();
        var categories = _storeService.GetCategories();
        var orders = _storeService.GetOrders();
        var utcNow = DateTime.UtcNow;
        const int lowStockThreshold = 5;

        var model = new AdminDashboardViewModel
        {
            TotalBooks = books.Count,
            TotalCategories = categories.Count,
            TotalOrders = orders.Count,
            TodayOrders = orders.Count(x => x.CreatedAt.Date == utcNow.Date),
            TotalRevenue = orders.Sum(x => x.TotalAmount),
            ThisMonthRevenue = orders
                .Where(x => x.CreatedAt.Year == utcNow.Year && x.CreatedAt.Month == utcNow.Month)
                .Sum(x => x.TotalAmount),
            LowStockThreshold = lowStockThreshold,
            LowStockBooks = books
                .Where(x => x.Stock <= lowStockThreshold)
                .OrderBy(x => x.Stock)
                .ThenBy(x => x.Title)
                .Take(8)
                .ToList(),
            RecentOrders = orders
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToList()
        };

        return View(model);
    }

    public IActionResult Index()
    {
        var categories = _storeService.GetCategories();
        var model = new AdminBooksViewModel
        {
            Books = _storeService.GetBooks(),
            CategoryNames = categories.ToDictionary(x => x.Id, x => x.Name)
        };

        return View(model);
    }

    public IActionResult Orders()
    {
        var orders = _storeService.GetOrders();
        return View(orders);
    }

    public IActionResult OrderDetails(int id)
    {
        var order = _storeService.GetOrder(id);
        if (order is null)
        {
            return NotFound();
        }

        return View(order);
    }

    public IActionResult Categories()
    {
        var model = new AdminCategoriesViewModel
        {
            Categories = _storeService.GetCategories(),
            BookCounts = _storeService.GetBookCountByCategory(),
            CreateInput = new AdminCategoryInputModel()
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult CreateCategory(AdminCategoryInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Tên danh mục không hợp lệ.";
            return RedirectToAction(nameof(Categories));
        }

        try
        {
            _storeService.AddCategory(input);
            TempData["SuccessMessage"] = "Đã thêm danh mục.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public IActionResult RenameCategory(int id, AdminCategoryInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Tên danh mục không hợp lệ.";
            return RedirectToAction(nameof(Categories));
        }

        try
        {
            var success = _storeService.UpdateCategory(id, input);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Đã cập nhật danh mục."
                : "Không tìm thấy danh mục.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public IActionResult DeleteCategory(int id)
    {
        var result = _storeService.DeleteCategory(id);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AdminBookFormViewModel
        {
            Categories = _storeService.GetCategories(),
            Input = new AdminBookInputModel()
        });
    }

    [HttpPost]
    public IActionResult Create(AdminBookInputModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(new AdminBookFormViewModel
            {
                Categories = _storeService.GetCategories(),
                Input = input
            });
        }

        _storeService.AddBook(input);
        TempData["SuccessMessage"] = "Đã thêm sách mới.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var book = _storeService.GetBook(id);
        if (book is null)
        {
            return NotFound();
        }

        var vm = new AdminBookFormViewModel
        {
            IsEdit = true,
            BookId = id,
            Categories = _storeService.GetCategories(),
            Input = new AdminBookInputModel
            {
                Title = book.Title,
                Author = book.Author,
                Price = book.Price,
                Stock = book.Stock,
                CategoryId = book.CategoryId,
                Description = book.Description,
                CoverUrl = book.CoverUrl
            }
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(int id, AdminBookInputModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(new AdminBookFormViewModel
            {
                IsEdit = true,
                BookId = id,
                Categories = _storeService.GetCategories(),
                Input = input
            });
        }

        if (!_storeService.UpdateBook(id, input))
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = "Đã cập nhật sách.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Delete(int id)
    {
        var book = _storeService.GetBook(id);
        if (book is null)
        {
            return NotFound();
        }

        return View(book);
    }

    [HttpPost]
    [ActionName("Delete")]
    public IActionResult DeleteConfirmed(int id)
    {
        var success = _storeService.DeleteBook(id);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
            ? "Đã xóa sách."
            : "Không thể xóa sách đã có trong đơn hàng hoặc không tồn tại.";

        return RedirectToAction(nameof(Index));
    }
}
