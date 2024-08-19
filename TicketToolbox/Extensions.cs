using System.Text.RegularExpressions;

namespace TicketToolbox;

public static class Extensions
{
    public static Uri ToUri(this string s) => new(s);
    public static Regex ToRegex(this string s) => new(s);
}
