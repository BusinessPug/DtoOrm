using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly ISchoolApiClient _api;

    public LoginModel(ISchoolApiClient api) => _api = api;

    [BindProperty]
    [Required]
    public string Username { get; set; } = "";

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(ReturnUrl));
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var login = await _api.LoginAsync(Username.Trim(), Password, cancellationToken);
        if (login is null)
        {
            ErrorMessage = "The username or password was not recognized.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.User.Id.ToString()),
            new(ClaimTypes.Name, login.User.DisplayName),
            new(ClaimTypes.Email, login.User.Email),
            new(ClaimTypes.Role, login.User.Role),
            new(PortalClaims.Username, login.User.Username),
            new(PortalClaims.AccessToken, login.AccessToken)
        };

        if (login.User.StudentId is not null)
        {
            claims.Add(new Claim(PortalClaims.StudentId, login.User.StudentId.Value.ToString()));
        }
        if (login.User.TeacherId is not null)
        {
            claims.Add(new Claim(PortalClaims.TeacherId, login.User.TeacherId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = login.ExpiresAt
            });

        return LocalRedirect(SafeReturnUrl(ReturnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
