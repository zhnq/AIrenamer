using System;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace AIRename.TextExtract;

internal sealed class DocxExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".docx", StringComparison.OrdinalIgnoreCase);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        using var fs = File.OpenRead(filePath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return string.Empty;
        using var s = entry.Open();
        var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true };
        using var reader = XmlReader.Create(s, settings);
        var result = new System.Text.StringBuilder(Math.Min(maxChars, 4096));
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
            {
                var text = reader.ReadElementContentAsString();
                if (!string.IsNullOrEmpty(text))
                {
                    result.Append(text);
                    result.Append(' ');
                    if (result.Length >= maxChars) break;
                }
            }
        }
        return result.ToString();
    }
}