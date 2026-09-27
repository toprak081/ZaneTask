using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZaneTask.Contracts;

/// <summary>
/// Project keys prefix task numbers (WEB-12). Shared by the API and the web app so both suggest the same key.
/// </summary>
public static partial class ProjectKeys
{
    public const int MinLength = 2;
    public const int MaxLength = 10;
    public const string Pattern = "^[A-Z][A-Z0-9]{1,9}$";
    public const string Rule = "2-10 letters or digits, starting with a letter (like WEB).";

    public static bool IsValid(string? key) => key is not null && KeyRegex().IsMatch(key);

    /// <summary>
    /// Suggests a key from a project name: initials for several words ("Mobile App" → MA,
    /// "Web Sitesi Projesi" → WSP), otherwise the first letters ("Website" → WEB).
    /// </summary>
    public static string Suggest(string? name)
    {
        var words = ToAscii(name ?? "")
            .ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsAsciiLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0 && char.IsAsciiLetter(w[0]))
            .ToList();

        var key = words.Count switch
        {
            0 => "",
            1 => words[0][..Math.Min(3, words[0].Length)],
            _ => string.Concat(words.Take(4).Select(w => w[0])),
        };

        if (key.Length < MinLength && words.Count > 0)
            key = words[0][..Math.Min(3, words[0].Length)];
        return key.Length >= MinLength ? key : "PRJ";
    }

    /// <summary>Turkish and other accented letters to plain A-Z (ş → s, ı → i, ü → u).</summary>
    private static string ToAscii(string text)
    {
        var decomposed = text.Replace('ı', 'i').Replace('İ', 'I').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString();
    }

    [GeneratedRegex(Pattern)]
    private static partial Regex KeyRegex();
}
