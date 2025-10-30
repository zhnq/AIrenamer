using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AIRename.Util;

public static class DeepSeekOcrInvoker
{
    public sealed class OcrResultInfo
    {
        public string Text { get; set; } = string.Empty;
        public int LoadMs { get; set; } = -1;
        public int InferMs { get; set; } = -1;
        public string? Device { get; set; }
        public string? Attn { get; set; }
        public string? ImagePath { get; set; }
    }

    public static string Run(string imagePath, int maxChars = 4000)
    {
        var info = RunWithInfo(imagePath, maxChars);
        return info.Text;
    }

    public static OcrResultInfo RunWithInfo(string imagePath, int maxChars = 4000)
    {
        var scriptPath = ResolveScriptPath();
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            return new OcrResultInfo();
        }

        var pythonExe = ResolvePythonExe();
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            // 使用 tiny 模型并强制 256 尺寸以提升速度
            Arguments = $"\"{scriptPath}\" --image \"{imagePath}\" --mode tiny --size 256 --max-chars {maxChars}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // 以脚本所在目录作为工作目录，确保相对资源解析更稳
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Directory.GetCurrentDirectory()
        };

        using var proc = Process.Start(psi);
        if (proc == null) return new OcrResultInfo();

        // 同步读取，确保完整文本与日志
        var stdoutText = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        var exited = proc.WaitForExit(180000);
        if (!exited)
        {
            try { proc.Kill(); } catch { }
            return new OcrResultInfo();
        }

        var text = (stdoutText ?? string.Empty).Trim();
        if (text.Length > maxChars) text = text.Substring(0, maxChars);

        var info = new OcrResultInfo { Text = text };
        // 解析时间日志
        if (!string.IsNullOrEmpty(stderr))
        {
            foreach (var line in stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // [time] load=1234ms device=cuda attn=flash_attention_2
                var m1 = Regex.Match(line, "^\\[time\\]\\s+load=(\\d+)ms\\s+device=([a-z0-9_]+)\\s+attn=([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                if (m1.Success)
                {
                    if (int.TryParse(m1.Groups[1].Value, out var lm)) info.LoadMs = lm;
                    info.Device = m1.Groups[2].Value;
                    info.Attn = m1.Groups[3].Value;
                    continue;
                }
                // [time] infer=456ms mode=small image='C:\\path\\img.png'
                var m2 = Regex.Match(line, "^\\[time\\]\\s+infer=(\\d+)ms\\s+mode=([a-zA-Z0-9_]+)\\s+image='(.+)'", RegexOptions.IgnoreCase);
                if (m2.Success)
                {
                    if (int.TryParse(m2.Groups[1].Value, out var im)) info.InferMs = im;
                    info.ImagePath = m2.Groups[3].Value;
                    continue;
                }
            }
        }

        return info;
    }

    private static string ResolveScriptPath()
    {
        var env = Environment.GetEnvironmentVariable("DEEPSEEK_OCR_PY");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        // Prefer alongside the executable directory (AppData\AIRename when installed)
        var exeDir = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(exeDir, "scripts", "deepseek_ocr_infer.py");
        if (File.Exists(candidate1)) return candidate1;
        // Fallback to current working directory (useful in dev runs)
        var candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "deepseek_ocr_infer.py");
        return candidate2;
    }

    private static string ResolvePythonExe()
    {
        var env = Environment.GetEnvironmentVariable("PYTHON");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        // 优先尝试 exe 目录下的本地虚拟环境 (.venv\Scripts\python.exe)
        var exeDir = AppContext.BaseDirectory;
        var venv1 = Path.Combine(exeDir, ".venv", "Scripts", "python.exe");
        if (File.Exists(venv1)) return venv1;

        // 回退到当前工作目录下的虚拟环境
        var cwd = Directory.GetCurrentDirectory();
        var venv2 = Path.Combine(cwd, ".venv", "Scripts", "python.exe");
        if (File.Exists(venv2)) return venv2;

        // 最后回退到系统 PATH 中的 python
        return "python";
    }
}