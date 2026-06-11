using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages;

/// <summary>
/// Base page model that centralises calls to the School API. Derived pages run their API work inside
/// <see cref="TryAsync"/> so a single, friendly banner is shown when the API or its database is down,
/// instead of an unhandled exception.
/// </summary>
public abstract class ApiPageModel : PageModel
{
    /// <summary>Populated when the API could not be reached; bound to the <c>_ApiError</c> partial.</summary>
    public string? ApiError { get; private set; }

    protected async Task<bool> TryAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            ApiError = "The School API is not reachable right now. Start the DtoOrm.Api project " +
                       "(and its MariaDB database), then refresh this page.";
            return false;
        }
    }
}
