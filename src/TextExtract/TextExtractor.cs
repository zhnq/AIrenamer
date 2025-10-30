using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AIRename.TextExtract;

public static class TextExtractor
{
    private static readonly List<ITextExtractor> Extractors = new()
    {
        new TxtExtractor(),
        new DocExtractor(),
        new DocxExtractor(),
        new PptxExtractor(),
        new ImageDeepSeekOcrExtractor(),
#if XLSX_SUPPORT
        new XlsxExtractor(),
#endif
#if PDF_SUPPORT
        new PdfExtractor()
#endif
    };

    public static string Extract(string filePath, int maxChars = 4000)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath is empty");
        if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var extractor = Extractors.FirstOrDefault(e => e.CanHandle(filePath));
        if (extractor == null) throw new NotSupportedException($"Unsupported file type: {ext}");
        return extractor.Extract(filePath, maxChars);
    }

    public static TitleCandidate GetLikelyTitle(string filePath, string text)
    {
        return TitleHeuristics.GetBestTitle(filePath, text);
    }

    public static string ExtractWithTitle(string filePath, int maxChars = 4000)
    {
        var body = Extract(filePath, maxChars);
        var candidate = TitleHeuristics.GetBestTitle(filePath, body);
        if (candidate.IsConfident && !string.IsNullOrWhiteSpace(candidate.Text))
        {
            return $"可能标题：{candidate.Text}\n\n" + body;
        }
        return body;
    }
}