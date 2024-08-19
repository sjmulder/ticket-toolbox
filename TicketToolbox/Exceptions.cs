using System.Diagnostics;

namespace TicketToolbox;

class UsageException(string message, Exception? inner = null)
    : Exception(message, inner);

class SubprocessException : Exception
{
    public SubprocessException(string message, Exception? inner = null)
        : base(message, inner) { }

    public SubprocessException(Process p)
        : base($"{p.ProcessName} exited with status {p.ExitCode}", null) { }
}
