using System.Diagnostics;
using System.Runtime.InteropServices;
using TicketToolbox.Tools;

// ReSharper disable MethodHasAsyncOverload

namespace TicketToolbox;

static class Program
{
    const int ExUsage = 64; // EX_USAGE from sysexits.h
    const int ExSubprocess = 123; // xargs' convention

    public static bool Verbose { get; private set; }
    public static bool DryRun { get; private set; }

    public static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "-?")
                throw new UsageException("no command given");
            if (args[0] != "link-commits")
                throw new UsageException($"bad command: {args[0]}");

            var rest = args[1..];

            for (; rest.Length > 0 && rest[0].StartsWith('-'); rest = rest[1..])
            {
                if (rest[0] == "-v" || rest[0] == "--interactive")
                    Verbose = true;
                else if (rest[0] == "-n" || rest[0] == "--dry-run")
                    DryRun = true;
                else
                    throw new UsageException($"bad option: {rest[0]}");
            }

            var settings = ToolSettings.LoadOrFail();
            var tool = new LinkCommitsTool(rest, settings);
            await tool.RunAsync();
        }
        catch (UsageException ex)
        {
            Console.Error.WriteLine($"ticket-toolbox: {ex.Message}");
            Console.Error.WriteLine("Usage: ticket-toolbox link-commits [refs]");

            Environment.Exit(ExUsage);
        }
        catch (SubprocessException ex)
        {
            Console.Error.WriteLine($"ticket-toolbox: {ex.Message}");

            Environment.Exit(ExSubprocess);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ticket-toolbox: {ex.Message}");

            Environment.Exit(1);
        }
    }

    public static string GetSecret(string envName, string friendlyName)
    {
        string? secret = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(secret))
            return secret;

        string? command = Environment.GetEnvironmentVariable($"{envName}_COMMAND");
        if (command != null)
        {
            using var process = new Process();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments = $"/c {command}";
            }
            else
            {
                process.StartInfo.FileName = "sh";
                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add(command);
            }

            if (Program.Verbose)
                Console.WriteLine($"+ {command}");

            process.Start();

            secret = process.StandardOutput.ReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
                Console.Error.WriteLine(
                    $"ticket-toolbox: '{command}' returned non-zero exit code " +
                    $"{process.ExitCode}");
            else if (string.IsNullOrWhiteSpace(secret))
                Console.Error.WriteLine(
                    $"ticket-toolbox: '{command}' returned no data");
            else
                return secret;
        }

        while (string.IsNullOrWhiteSpace(secret))
        {
            Console.Write($"{friendlyName}: ");
            Console.Out.Flush();

            secret = Console.ReadLine();
        }

        return secret;
    }
}
