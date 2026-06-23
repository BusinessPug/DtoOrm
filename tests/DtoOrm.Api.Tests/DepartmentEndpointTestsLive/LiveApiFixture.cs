using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DtoOrm.Api.Features.Auth;
using Xunit;
using Xunit.Sdk;

namespace DtoOrm.Api.Tests.DepartmentEndpointTestsLive;

public sealed class LiveApiFixture : IAsyncLifetime
{
    public Uri ApiBaseUri { get; } = new(
        Environment.GetEnvironmentVariable("DTOORM_LIVE_API_BASE_URL") ?? "http://localhost:5080");

    public string DatabaseConnectionString { get; } =
        Environment.GetEnvironmentVariable("DTOORM_LIVE_DB_CONNECTION")
        ?? "Server=localhost;Port=3306;Database=dtoorm_demo;User Id=dtoorm;Password=dtoorm;AllowPublicKeyRetrieval=true;SslMode=None";

    public HttpClient CreateClient() => new() { BaseAddress = ApiBaseUri };

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("admin", "School123!")).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new XunitException(
                    $"Login failed with {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{body}");
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>().ConfigureAwait(false);
            if (login is null || string.IsNullOrWhiteSpace(login.AccessToken))
            {
                throw new XunitException("Login succeeded, but the response did not contain an access token.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        if (await IsApiReadyAsync().ConfigureAwait(false))
            return;

        var repoRoot = FindRepoRoot();
        var startScript = Path.Combine(repoRoot, "start-all.ps1");
        var shell = ResolvePowerShell();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = shell,
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(startScript);
        process.StartInfo.ArgumentList.Add("-NoBrowser");

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new XunitException($"start-all.ps1 failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");

        if (!await IsApiReadyAsync().ConfigureAwait(false))
            throw new XunitException($"start-all.ps1 completed, but the API did not answer at {ApiBaseUri}.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> IsApiReadyAsync()
    {
        try
        {
            using var client = CreateClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            using var response = await client.GetAsync("/swagger/v1/swagger.json").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var startScript = Path.Combine(directory.FullName, "start-all.ps1");
            if (File.Exists(startScript))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new XunitException("Could not find the repository root containing start-all.ps1.");
    }

    private static string ResolvePowerShell()
        => CommandExists("pwsh") ? "pwsh" : "powershell";

    private static bool CommandExists(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                ArgumentList = { "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
                return false;

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
