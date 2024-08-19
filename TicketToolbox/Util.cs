using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TicketToolbox;

static class Util
{
    public const int ExUsage = 64; // EX_USAGE from sysexits.h
    public const int ExSubprocess = 123; // xargs' convention

    [DoesNotReturn]
    public static void Fail(string message, int exitCode = 1)
    {
        Console.Error.WriteLine($"ticket-toolbox: {message}");
        Environment.Exit(exitCode);
    }

    public static T DoOrFail<T>(Func<T> fn, string message)
    {
        try
        {
            return fn();
        }
        catch (Exception e)
        {
            Util.Fail($"{message}: {e.Message}");
            throw; // not reached
        }
    }

    public static string GetEnvOrFail(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (value == null) Util.Fail($"{name} must be set");

        return value;
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
                Console.Error.WriteLine($"ticket-toolbox: '{command}' returned no data");
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
