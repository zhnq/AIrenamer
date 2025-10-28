using System;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace AIRename.TextExtract;

internal sealed class PptxExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pptx", StringComparison.OrdinalIgnoreCase);
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        using var fs = File.OpenRead(filePath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        var sb = new System.Text.StringBuilder(Math.Min(maxChars, 4096));
        int slideIndex = 1;
        while (sb.Length < maxChars)
        {
            var entry = zip.GetEntry($"ppt/slides/slide{slideIndex}.xml");
            if (entry == null) break;
            using var s = entry.Open();
            var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true };
            using var reader = XmlReader.Create(s, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
                {
                    var text = reader.ReadElementContentAsString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.Append(text);
                        sb.Append(' ');
                        if (sb.Length >= maxChars) break;
                    }
                }
            }
            slideIndex++;
        }
        return sb.ToString();
    }
}