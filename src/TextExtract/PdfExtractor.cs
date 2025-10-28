using System;
using System.IO;
using UglyToad.PdfPig;

namespace AIRename.TextExtract;

internal sealed class PdfExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        using var doc = PdfDocument.Open(filePath);
        var sb = new System.Text.StringBuilder(Math.Min(maxChars, 4096));
        foreach (var page in doc.GetPages())
        {
            if (sb.Length >= maxChars) break;
            var text = page.Text;
            if (!string.IsNullOrEmpty(text))
            {
                if (text.Length > maxChars - sb.Length)
                {
                    sb.Append(text.AsSpan(0, maxChars - sb.Length));
                }
                else
                {
                    sb.Append(text);
                }
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }
}