using System;
using System.IO;
using AIRename.Util;

namespace AIRename.TextExtract;

internal sealed class ImageDeepSeekOcrExtractor : ITextExtractor
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".webp";
    }

    public string Extract(string filePath, int maxChars = 4000)
    {
        try
        {
            return DeepSeekOcrInvoker.Run(filePath, maxChars);
        }
        catch
        {
            return string.Empty;
        }
    }
}