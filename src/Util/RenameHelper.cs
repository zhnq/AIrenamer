using System;
using System.IO;
using System.Linq;

namespace AIRename.Util;

public static class RenameHelper
{
    private static readonly char[] IllegalChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    public static string SanitizeFileName(string name, int maxLen = 60)
    {
        if (string.IsNullOrWhiteSpace(name)) return "untitled";
        var filtered = new string(name.Where(ch => !IllegalChars.Contains(ch)).ToArray());
        filtered = filtered.Trim();
        // 去除尾部句点或空格
        filtered = filtered.TrimEnd(' ', '.');
        if (filtered.Length == 0) filtered = "untitled";
        if (filtered.Length > maxLen) filtered = filtered.Substring(0, maxLen);
        return filtered;
    }

    public static string EnsureUniquePath(string dir, string stem, string ext)
    {
        var path = Path.Combine(dir, stem + ext);
        if (!File.Exists(path)) return path;
        int i = 1;
        while (true)
        {
            var candidate = Path.Combine(dir, $"{stem} (_{i}){ext}");
            if (!File.Exists(candidate)) return candidate;
            i++;
        }
    }

    public static bool TryRename(string srcPath, string newStem, out string newPath)
    {
        newPath = srcPath;
        try
        {
            var dir = Path.GetDirectoryName(srcPath)!;
            var ext = Path.GetExtension(srcPath);
            var safeStem = SanitizeFileName(newStem);
            var target = EnsureUniquePath(dir, safeStem, ext);
            File.Move(srcPath, target);
            newPath = target;
            return true;
        }
        catch { return false; }
    }
}