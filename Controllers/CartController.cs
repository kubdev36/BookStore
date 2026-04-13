using BookStore.Extensions;
using BookStore.Models;
using BookStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Controllers;

public class CartController : Controller
{
    private const string CartSessionKey = "bookstore_cart";
    private readonly IStoreService _storeService;
    private readonly IConfiguration _configuration;

    public CartController(IStoreService storeService, IConfiguration configuration)
    {
        _storeService = storeService;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View(GetCart());
    }

    [HttpPost]
    public IActionResult Add(int bookId, int quantity = 1)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            TempData["ErrorMessage"] = "Vui lòng đăng nhập để thêm sách vào giỏ hàng.";
            return RedirectToAction("Login", "Account", new { returnUrl = GetSafeReturnUrl(bookId) });
        }

        var book = _storeService.GetBook(bookId);
        if (book is null)
        {
            return NotFound();
        }

        if (book.Stock <= 0)
        {
            TempData["ErrorMessage"] = "Sách đã hết hàng.";
            return RedirectToAction("Index", "Books");
        }

        var cart = GetCart();
        var line = cart.FirstOrDefault(x => x.BookId == bookId);
        var requestedQuantity = Math.Max(1, quantity);

        if (line is null)
        {
            if (requestedQuantity > book.Stock)
            {
                TempData["ErrorMessage"] = $"Chỉ còn {book.Stock} quyển trong kho.";
                return RedirectToAction("Index", "Books");
            }

            cart.Add(new CartItemViewModel
            {
                BookId = book.Id,
                Title = book.Title,
                CoverUrl = book.CoverUrl,
                Price = book.Price,
                Quantity = requestedQuantity
            });
        }
        else
        {
            var newQuantity = line.Quantity + requestedQuantity;
            if (newQuantity > book.Stock)
            {
                TempData["ErrorMessage"] = $"Chỉ còn {book.Stock} quyển trong kho.";
                return RedirectToAction("Index", "Books");
            }

            line.Quantity = newQuantity;
            if (string.IsNullOrWhiteSpace(line.CoverUrl))
            {
                line.CoverUrl = book.CoverUrl;
            }
        }

        SaveCart(cart);
        TempData["SuccessMessage"] = "Đã thêm sách vào giỏ hàng.";

        return RedirectToAction("Index", "Books");
    }

    private string GetSafeReturnUrl(int bookId)
    {
        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            var localPath = refererUri.PathAndQuery;
            if (Url.IsLocalUrl(localPath))
            {
                return localPath;
            }
        }

        return Url.Action("Details", "Books", new { id = bookId })
            ?? Url.Action("Index", "Books")
            ?? "/";
    }

    [HttpPost]
    public IActionResult UpdateQuantity(int bookId, int quantity)
    {
        var cart = GetCart();
        var line = cart.FirstOrDefault(x => x.BookId == bookId);

        if (line is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (quantity <= 0)
        {
            cart.Remove(line);
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        var book = _storeService.GetBook(bookId);
        if (book is null)
        {
            cart.Remove(line);
            SaveCart(cart);
            TempData["ErrorMessage"] = "Sản phẩm không còn tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        if (quantity > book.Stock)
        {
            TempData["ErrorMessage"] = $"Chỉ còn {book.Stock} quyển trong kho.";
            return RedirectToAction(nameof(Index));
        }

        line.Quantity = quantity;
        SaveCart(cart);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(int bookId)
    {
        var cart = GetCart();
        var line = cart.FirstOrDefault(x => x.BookId == bookId);

        if (line is not null)
        {
            cart.Remove(line);
            SaveCart(cart);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Clear()
    {
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            _storeService.ClearCart(userId);
        }

        HttpContext.Session.Remove(CartSessionKey);
        TempData["SuccessMessage"] = "Đã xóa toàn bộ giỏ hàng.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet]
    public IActionResult Checkout()
    {
        var cart = GetCart();
        if (cart.Count == 0)
        {
            TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
            return RedirectToAction(nameof(Index));
        }

        var validationError = ValidateStock(cart);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            TempData["ErrorMessage"] = validationError;
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CartItems = cart;
        ViewBag.TotalAmount = cart.Sum(x => x.LineTotal);

        return View(new CheckoutInputModel());
    }

    [Authorize]
    [HttpPost]
    public IActionResult Checkout(CheckoutInputModel input)
    {
        var cart = GetCart();
        if (cart.Count == 0)
        {
            TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
            return RedirectToAction(nameof(Index));
        }

        var validationError = ValidateStock(cart);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            TempData["ErrorMessage"] = validationError;
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CartItems = cart;
            ViewBag.TotalAmount = cart.Sum(x => x.LineTotal);
            return View(input);
        }

        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            var normalizedPaymentMethod = NormalizePaymentMethod(input.PaymentMethod);
            input.PaymentMethod = normalizedPaymentMethod;
            var paymentNote = normalizedPaymentMethod == PaymentMethods.VietQr
                ? "Khach hang thanh toan chuyen khoan VietQR."
                : null;

            var orderId = _storeService.CreateOrder(input, cart, userId, paymentNote);
            if (userId.HasValue)
            {
                _storeService.ClearCart(userId.Value);
            }

            HttpContext.Session.Remove(CartSessionKey);

            if (normalizedPaymentMethod == PaymentMethods.VietQr)
            {
                return RedirectToAction(nameof(VietQrPayment), new { id = orderId });
            }

            return RedirectToAction(nameof(OrderSuccess), new { id = orderId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize]
    public IActionResult OrderSuccess(int id)
    {
        ViewBag.OrderId = id;
        return View();
    }

    [Authorize]
    [HttpGet]
    public IActionResult VietQrPayment(int id)
    {
        var order = _storeService.GetOrder(id);
        if (order is null)
        {
            return NotFound();
        }

        if (!string.Equals(order.PaymentMethod, PaymentMethods.VietQr, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(OrderSuccess), new { id });
        }

        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (int?)null;
        var isAdmin = string.Equals(User.FindFirstValue(ClaimTypes.Role), UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && currentUserId.HasValue && order.UserId.HasValue && currentUserId.Value != order.UserId.Value)
        {
            return Forbid();
        }

        var bankId = _configuration["VietQr:BankId"] ?? string.Empty;
        var accountNo = _configuration["VietQr:AccountNumber"] ?? string.Empty;
        var accountName = _configuration["VietQr:AccountName"] ?? string.Empty;
        var template = _configuration["VietQr:Template"] ?? "compact2";

        if (string.IsNullOrWhiteSpace(bankId) || string.IsNullOrWhiteSpace(accountNo) || string.IsNullOrWhiteSpace(accountName))
        {
            TempData["ErrorMessage"] = "Chua cau hinh thong tin VietQR. Vui long lien he quan tri vien.";
            return RedirectToAction(nameof(OrderSuccess), new { id });
        }

        var transferContent = $"THANH TOAN DON {order.Id}";
        var qrUrl = BuildVietQrImageUrl(bankId, accountNo, template, order.TotalAmount, transferContent, accountName);

        ViewBag.OrderId = order.Id;
        ViewBag.Amount = order.TotalAmount;
        ViewBag.BankId = bankId;
        ViewBag.AccountNumber = accountNo;
        ViewBag.AccountName = accountName;
        ViewBag.TransferContent = transferContent;
        ViewBag.QrImageUrl = qrUrl;

        return View();
    }

    private string? ValidateStock(List<CartItemViewModel> cart)
    {
        foreach (var item in cart)
        {
            var book = _storeService.GetBook(item.BookId);
            if (book is null)
            {
                return $"Sách '{item.Title}' đã bị gỡ khỏi cửa hàng.";
            }

            if (item.Quantity > book.Stock)
            {
                return $"Sách '{item.Title}' chỉ còn {book.Stock} quyển trong kho.";
            }
        }

        return null;
    }

    private List<CartItemViewModel> GetCart()
    {
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return _storeService.GetCartItems(userId).ToList();
        }

        return HttpContext.Session.GetObject<List<CartItemViewModel>>(CartSessionKey) ?? [];
    }

    private void SaveCart(List<CartItemViewModel> cart)
    {
        if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            _storeService.SyncCartItems(userId, cart);
            return;
        }

        HttpContext.Session.SetObject(CartSessionKey, cart);
    }

    private static string NormalizePaymentMethod(string? paymentMethod)
    {
        if (string.Equals(paymentMethod, PaymentMethods.VietQr, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentMethods.VietQr;
        }

        return PaymentMethods.CashOnDelivery;
    }

    private static string BuildVietQrImageUrl(
        string bankId,
        string accountNumber,
        string template,
        decimal amount,
        string transferContent,
        string accountName)
    {
        var normalizedBankId = bankId.Trim();
        var normalizedAccountNumber = accountNumber.Trim();
        var normalizedTemplate = string.IsNullOrWhiteSpace(template) ? "compact2" : template.Trim();
        var amountParam = Convert.ToInt64(Math.Round(amount, MidpointRounding.AwayFromZero));
        var addInfoParam = Uri.EscapeDataString(transferContent.Trim());
        var accountNameParam = Uri.EscapeDataString(accountName.Trim());
        return $"https://img.vietqr.io/image/{normalizedBankId}-{normalizedAccountNumber}-{normalizedTemplate}.png?amount={amountParam}&addInfo={addInfoParam}&accountName={accountNameParam}";
    }
}
