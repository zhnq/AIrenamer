using System;
using System.IO;
using System.Text;
using Spire.Doc;

namespace AIRename.TextExtract;

internal sealed class DocExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".doc", StringComparison.OrdinalIgnoreCase);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        var document = new Document();
        document.LoadFromFile(filePath);
        var raw = document.GetText();
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var cleaned = Sanitize(raw);
        if (cleaned.Length <= maxChars) return cleaned;

        var sb = new StringBuilder(Math.Min(maxChars, 4096));
        sb.Append(cleaned.AsSpan(0, maxChars));
        return sb.ToString();
    }

    private static string Sanitize(string s)
    {
        // Normalize whitespace and drop typical control characters
        var cleaned = s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
                       .Replace('\a', ' ').Replace('\f', ' ').Replace('\v', ' ')
                       .Replace('\u0007', ' ');
        return cleaned.Trim();
    }
}