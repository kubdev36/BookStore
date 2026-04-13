using BookStore.Models;
using BookStore.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BookStore.Controllers;

public class AccountController : Controller
{
    private const string ExternalAuthScheme = "ExternalOAuth";
    private static readonly Dictionary<string, string> ExternalProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Google"] = "Google",
        ["Facebook"] = "Facebook"
    };

    private readonly IStoreService _storeService;

    public AccountController(IStoreService storeService)
    {
        _storeService = storeService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Books");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new RegisterInputModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterInputModel input, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        var result = _storeService.RegisterUser(input, UserRoles.User);
        if (!result.Success || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        TempData["SuccessMessage"] = "Đăng ký thành công. Vui lòng đăng nhập để tiếp tục.";
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectAuthenticatedUserHome();
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginInputModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel input, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        var user = _storeService.ValidateUser(input.Email, input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        await SignInUser(user, input.RememberMe);
        TempData["SuccessMessage"] = "Đăng nhập thành công.";

        return RedirectToLocal(returnUrl, user);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
    {
        if (!ExternalProviders.TryGetValue(provider, out var scheme))
        {
            TempData["ErrorMessage"] = "Phương thức đăng nhập không được hỗ trợ.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var schemeProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var configuredScheme = await schemeProvider.GetSchemeAsync(scheme);
        if (configuredScheme is null)
        {
            TempData["ErrorMessage"] = $"Đăng nhập bằng {scheme} chưa được cấu hình.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl, provider = scheme })
            ?? Url.Action(nameof(Login));

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, scheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? provider = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            TempData["ErrorMessage"] = $"Đăng nhập ngoài thất bại: {remoteError}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (string.IsNullOrWhiteSpace(provider) || !ExternalProviders.TryGetValue(provider, out var providerName))
        {
            TempData["ErrorMessage"] = "Không xác định được nhà cung cấp đăng nhập.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var authResult = await HttpContext.AuthenticateAsync(ExternalAuthScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            TempData["ErrorMessage"] = "Không thể lấy thông tin tài khoản từ nhà cung cấp.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        try
        {
            var email = authResult.Principal.FindFirstValue(ClaimTypes.Email)
                ?? authResult.Principal.FindFirstValue("email");

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Tài khoản đăng nhập ngoài chưa cung cấp email.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var fullName = authResult.Principal.FindFirstValue(ClaimTypes.Name)
                ?? email.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? "Người dùng";

            var user = _storeService.GetUserByEmail(email);
            if (user is null)
            {
                var randomPassword = GenerateExternalPassword();
                var registerResult = _storeService.RegisterUser(new RegisterInputModel
                {
                    FullName = fullName,
                    Email = email,
                    Password = randomPassword,
                    ConfirmPassword = randomPassword
                }, UserRoles.User);

                if (!registerResult.Success || registerResult.User is null)
                {
                    TempData["ErrorMessage"] = registerResult.Message;
                    return RedirectToAction(nameof(Login), new { returnUrl });
                }

                user = registerResult.User;
            }

            await SignInUser(user, rememberMe: true);
            TempData["SuccessMessage"] = $"Đăng nhập bằng {providerName} thành công.";
            return RedirectToLocal(returnUrl, user);
        }
        finally
        {
            await HttpContext.SignOutAsync(ExternalAuthScheme);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "Đã đăng xuất.";
        return RedirectToAction("Index", "Books");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInUser(UserAccount user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    private IActionResult RedirectToLocal(string? returnUrl, UserAccount? signedInUser = null)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (string.Equals(signedInUser?.Role, UserRoles.Admin, StringComparison.Ordinal))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        if (User.Identity?.IsAuthenticated == true && User.IsInRole(UserRoles.Admin))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        return RedirectToAction("Index", "Books");
    }

    private IActionResult RedirectAuthenticatedUserHome()
    {
        return User.IsInRole(UserRoles.Admin)
            ? RedirectToAction("Dashboard", "Admin")
            : RedirectToAction("Index", "Books");
    }

    private static string GenerateExternalPassword()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        return $"{raw}Aa1!";
    }
}
