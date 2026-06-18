using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("DtoOrm.Portal");

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AuthorizePage("/Students/Index", "AdminOrTeacher");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.Name = "DtoOrm.School.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator"));
    options.AddPolicy("AdminOrTeacher", policy => policy.RequireRole("Administrator", "Teacher"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthenticatedApiHandler>();

// The portal talks to the School API exclusively over HTTP through a typed client. Pages depend on
// the ISchoolApiClient abstraction, never on the concrete implementation or the API project itself.
var apiBaseUrl = builder.Configuration["SchoolApi:BaseUrl"]
    ?? throw new InvalidOperationException("SchoolApi:BaseUrl is required.");

builder.Services.AddHttpClient<ISchoolApiClient, SchoolApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler<AuthenticatedApiHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
