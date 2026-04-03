using System.Diagnostics;
using System.Text;
using Amnezia.Agent.Configuration;
using Microsoft.Extensions.Options;

namespace Amnezia.Agent.Services;

public sealed class ShellCommandRunner(IOptions<AgentSecurityOptions> options)
{
    private readonly AgentSecurityOptions _options = options.Value;

    public async Task<string> RunAsync(string command, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.CommandTimeoutSeconds)));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                ArgumentList = { "-lc", command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start shell command: {command}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill errors on timeout/cancel.
            }

            throw new TimeoutException($"Command timed out after {_options.CommandTimeoutSeconds} seconds: {command}");
        }

        if (process.ExitCode != 0)
        {
            var message = stderr.Length > 0 ? stderr.ToString().Trim() : stdout.ToString().Trim();
            throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}: {message}");
        }

        return stdout.ToString().Trim();
    }
}
