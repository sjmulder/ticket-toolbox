namespace TicketToolbox;

class UsageException(string message, Exception? inner = null) : Exception(message, inner);
