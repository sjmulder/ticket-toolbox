using System.Text.RegularExpressions;

namespace TicketToolbox;

public static class Extensions
{
    public static Uri ToUri(this string s) => new(s);
    public static Regex ToRegex(this string s) => new(s);
    public static Guid ToGuid(this string s) => Guid.Parse(s);

    public static IEnumerable<(int i, T elem)> Ordinate<T>(
        this IEnumerable<T> xs)
    {
        int i = 0;

        foreach (var x in xs)
            yield return (i++, x);
    }
}
