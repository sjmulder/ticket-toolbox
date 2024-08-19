using TicketToolbox.Tools;

namespace TicketToolbox;

static class Program
{
    public static bool Verbose { get; private set; }
    public static bool DryRun { get; private set; }

    public static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "-?")
                Usage("no command given");
            if (args[0] != "link-commits")
                Usage($"bad command: {args[0]}");

            args = args[1..];

            for (; args.Length > 0 && args[0].StartsWith('-'); args = args[1..])
            {
                if (args[0] == "-v" || args[0] == "--interactive")
                    Verbose = true;
                else if (args[0] == "-n" || args[0] == "--dry-run")
                    DryRun = true;
                else
                    Util.Fail($"bad option: {args[0]}");
            }

            var settings = ToolSettings.LoadOrFail();
            var tool = new LinkCommitsTool(args, settings);
            await tool.RunAsync();
        }
        catch (UsageException ex)
        {
            Usage(ex.Message);
        }
        catch (Exception ex)
        {
            Util.Fail(ex.Message);
        }
    }

    static void Usage(string message)
    {
        Console.Error.WriteLine($"ticket-toolbox: {message}");
        Console.Error.WriteLine("Usage: ticket-toolbox link-commits [refs]");
        Environment.Exit(Util.ExUsage);
    }
}
