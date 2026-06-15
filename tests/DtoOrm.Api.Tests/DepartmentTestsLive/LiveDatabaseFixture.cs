using System.Diagnostics;
using DtoOrm.Core;
using DtoOrm.MariaDb;
using MySqlConnector;
using Xunit;
using Xunit.Sdk;

namespace DtoOrm.Api.Tests.DepartmentTestsLive;

public sealed class LiveDatabaseFixture : IAsyncLifetime
{
    public string DatabaseConnectionString { get; } =
        Environment.GetEnvironmentVariable("DTOORM_LIVE_DB_CONNECTION")
        ?? "Server=localhost;Port=3306;Database=dtoorm_demo;User Id=dtoorm;Password=dtoorm;AllowPublicKeyRetrieval=true;SslMode=None";

    public OrmSession CreateSession() => MariaDbOrm.Create(DatabaseConnectionString);

    public async Task InitializeAsync()
    {
        if (await IsDatabaseReadyAsync().ConfigureAwait(false))
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

        if (!await IsDatabaseReadyAsync().ConfigureAwait(false))
            throw new XunitException("start-all.ps1 completed, but the database did not accept connections.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<bool> IsDatabaseReadyAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
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
