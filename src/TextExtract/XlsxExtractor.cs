using System;
using System.IO;
using ExcelDataReader;

namespace AIRename.TextExtract;

internal sealed class XlsxExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(fs);
        var sb = new System.Text.StringBuilder(Math.Min(maxChars, 4096));
        do
        {
            while (reader.Read() && sb.Length < maxChars)
            {
                for (int i = 0; i < reader.FieldCount && sb.Length < maxChars; i++)
                {
                    var val = reader.GetValue(i)?.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        sb.Append(val);
                        sb.Append(' ');
                    }
                }
            }
        } while (reader.NextResult() && sb.Length < maxChars);
        return sb.ToString();
    }
}