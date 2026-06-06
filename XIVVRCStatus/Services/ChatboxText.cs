using System.Globalization;
using System.Linq;

namespace XIVVRCStatus.Services;

public static class ChatboxText
{
    public const int MaxCharacters = 144;
    public const int MaxLines = 9;

    public static string Sanitize(string text)
    {
        var normalized = text
            .Replace("\0", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var lines = normalized
            .Split('\n')
            .Select(CleanLine)
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length > MaxLines)
        {
            normalized = string.Join("\n", lines.Take(MaxLines));
        }
        else
        {
            normalized = string.Join("\n", lines);
        }

        var textInfo = new StringInfo(normalized);
        return textInfo.LengthInTextElements > MaxCharacters
            ? textInfo.SubstringByTextElements(0, MaxCharacters)
            : normalized;
    }

    private static string CleanLine(string line)
    {
        var cleaned = line.Trim();
        while (HasTrailingSeparator(cleaned))
        {
            cleaned = cleaned[..^1].TrimEnd();
        }

        return cleaned;
    }

    private static bool HasTrailingSeparator(string line)
    {
        return line.EndsWith('|')
            || line.EndsWith('｜')
            || line.EndsWith('/')
            || line.EndsWith('-');
    }
}
