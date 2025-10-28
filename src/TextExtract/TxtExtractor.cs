using System;
using System.IO;
using System.Text;

namespace AIRename.TextExtract;

internal sealed class TxtExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".txt" || string.IsNullOrEmpty(ext);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        var sb = new StringBuilder(Math.Min(maxChars, 4096));
        using var sr = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[2048];
        int read;
        while ((read = sr.Read(buffer, 0, Math.Min(buffer.Length, maxChars - sb.Length))) > 0)
        {
            sb.Append(buffer, 0, read);
            if (sb.Length >= maxChars) break;
        }
        return sb.ToString();
    }
}