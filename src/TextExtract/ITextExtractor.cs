using System.IO;

namespace AIRename.TextExtract;

public interface ITextExtractor
{
    string Extract(string filePath, int maxChars = 4000);
    bool CanHandle(string filePath);
}