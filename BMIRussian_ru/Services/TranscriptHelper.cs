using System.Text.RegularExpressions;

namespace BMIRussian_ru.Services;

public static class TranscriptHelper
{
    /// <summary>Extracts plain text from ASS/SSA subtitle content. Returns content as-is for other formats.</summary>
    public static string ExtractPlainText(string? content, string? fileExtension = null)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var ext = (fileExtension ?? "").ToLowerInvariant();
        var isAss = ext is ".ass" or ".ssa" || content.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Dialogue:", StringComparison.Ordinal);

        if (!isAss)
            return content;

        var lines = content.Split('\n', '\r');
        var textParts = new List<string>();

        foreach (var line in lines)
        {
            var dialogueIdx = line.IndexOf("Dialogue:", StringComparison.OrdinalIgnoreCase);
            if (dialogueIdx < 0)
                continue;

            var rest = line[(dialogueIdx + 9)..].TrimStart();
            var commas = 0;
            var textStart = 0;
            for (var i = 0; i < rest.Length; i++)
            {
                if (rest[i] == ',')
                {
                    commas++;
                    if (commas >= 9)
                    {
                        textStart = i + 1;
                        break;
                    }
                }
            }

            if (commas < 9)
                continue;

            var text = rest[textStart..];
            text = StripAssOverrideCodes(text);
            if (!string.IsNullOrWhiteSpace(text))
                textParts.Add(text.Trim());
        }

        return string.Join("\n", textParts);
    }

    /// <summary>Strips ASS override codes like {\i1}, {\b1}, {\fnFont}, etc.</summary>
    private static string StripAssOverrideCodes(string text)
    {
        return Regex.Replace(text, @"\{[^}]*\}", "");
    }
}
