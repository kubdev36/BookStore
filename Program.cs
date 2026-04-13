using BookStore.Models;
using BookStore.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
const string externalScheme = "ExternalOAuth";

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddCookie(externalScheme);

var googleAuth = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = googleAuth["ClientId"]?.Trim();
var googleClientSecret = googleAuth["ClientSecret"]?.Trim();
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle("Google", options =>
        {
            options.SignInScheme = externalScheme;
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

var facebookAuth = builder.Configuration.GetSection("Authentication:Facebook");
var facebookAppId = facebookAuth["AppId"]?.Trim();
var facebookAppSecret = facebookAuth["AppSecret"]?.Trim();
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    builder.Services.AddAuthentication()
        .AddFacebook("Facebook", options =>
        {
            options.SignInScheme = externalScheme;
            options.AppId = facebookAppId;
            options.AppSecret = facebookAppSecret;
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRoles.Admin));
});

builder.Services.AddScoped<IStoreService, SqlServerStoreService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Books}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
