using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Linq;
using AIRename.TextExtract;
using AIRename.Summarize;
using AIRename.UI;
using AIRename.Util;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr GetForegroundWindow();

    private static class Logger
    {
        private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIRename", "app.log");
        private static readonly Encoding LogEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        private static void EnsureLogFileWithBom()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (!File.Exists(LogPath))
                {
                    File.WriteAllText(LogPath, string.Empty, LogEncoding);
                    return;
                }
                using var fs = File.OpenRead(LogPath);
                var hasBom = false;
                if (fs.Length >= 3)
                {
                    var b = new byte[3];
                    _ = fs.Read(b, 0, 3);
                    hasBom = b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
                }
                if (!hasBom)
                {
                    var text = File.ReadAllText(LogPath, Encoding.UTF8);
                    File.WriteAllText(LogPath, text, LogEncoding);
                }
            }
            catch { /* 任何日志失败均忽略，避免影响主流程 */ }
        }

        public static void Log(string message)
        {
            try
            {
                EnsureLogFileWithBom();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}", LogEncoding);
            }
            catch { /* 任何日志失败均忽略，避免影响主流程 */ }
        }
    }

    private static int Main(string[] args)
    {
        Logger.Log("启动 | args=" + string.Join(" ", args ?? Array.Empty<string>()));
        var hwnd = GetForegroundWindow();
        const int MB_ICONINFORMATION = 0x00000040;
        const int MB_TOPMOST = 0x00040000;
        const int MB_SYSTEMMODAL = 0x00001000;

        // 若传入了文件路径（来自右键菜单 "%1" 或命令行），执行文本抽取并展示验证信息
        if (args != null && args.Length > 0)
        {
            var path = args[0];
            try
            {
                if (!File.Exists(path))
                {
                    MessageBox(hwnd, "参数文件不存在", "AI Rename", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
                    Logger.Log("文件不存在: " + path);
                }
                else
                {
                    var proc = Process.GetCurrentProcess();
                    var wsStartMb = proc.WorkingSet64 / (1024.0 * 1024.0);
                    var sw = Stopwatch.StartNew();
                    var maxChars = GetEnvInt("AIRENAME_MAX_CHARS", 2000);
                    DeepSeekOcrInvoker.OcrResultInfo? ocrInfo = null;
                    var extLower = Path.GetExtension(path).ToLowerInvariant();
                    var isImage = extLower is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".webp";
                    var body = string.Empty;
                    if (isImage)
                    {
                        ocrInfo = DeepSeekOcrInvoker.RunWithInfo(path, maxChars);
                        body = ocrInfo.Text ?? string.Empty;
                    }
                    else
                    {
                        body = TextExtractor.Extract(path, maxChars: maxChars);
                    }
                    var title = TextExtractor.GetLikelyTitle(path, body);
                    var text = (title.IsConfident && !string.IsNullOrWhiteSpace(title.Text))
                        ? $"可能标题：{title.Text}\n\n{body}"
                        : body;
                    sw.Stop();
                    proc.Refresh();
                    var wsEndMb = proc.WorkingSet64 / (1024.0 * 1024.0);
                    var msg = new StringBuilder();
                    msg.AppendLine($"文件: {Path.GetFileName(path)}");
                    msg.AppendLine($"长度: {text?.Length ?? 0} 字符");
                    msg.AppendLine($"耗时: {sw.ElapsedMilliseconds} ms");
                    if (ocrInfo != null && (ocrInfo.LoadMs >= 0 || ocrInfo.InferMs >= 0))
                    {
                        msg.AppendLine($"OCR: 加载 {Math.Max(ocrInfo.LoadMs, 0)} ms, 推理 {Math.Max(ocrInfo.InferMs, 0)} ms");
                    }
                    msg.AppendLine($"内存: ~{Math.Round(wsEndMb, 1)} MB");
                    var preview = text;
                    if (!string.IsNullOrEmpty(preview))
                    {
                        preview = preview.Length > 256 ? preview.Substring(0, 256) + "…" : preview;
                        msg.AppendLine("");
                        msg.AppendLine("预览:");
                        msg.AppendLine(preview);
                    }
                    MessageBox(hwnd, msg.ToString(), "AI Rename | 文本抽取验证", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
                    Logger.Log($"抽取完成 | len={text?.Length ?? 0}, time={sw.ElapsedMilliseconds}ms, ws={Math.Round(wsEndMb,1)}MB");

                    // 基于正文抽取 Top20 关键词，生成 3~5 个候选名
                    var top = TextRank.ExtractTopN(body, topN: 20).Select(p => p.Token).ToList();
                    var stemFromTitle = title.IsConfident ? title.Text : Path.GetFileNameWithoutExtension(path);
                    var candidates = BuildCandidates(stemFromTitle, top);

                    if (candidates.Count > 0)
                    {
                        var idx = PopupMenuSelector.Show(IntPtr.Zero, candidates.ToArray(), "AI Rename | 推荐文件名");
                        if (idx >= 0)
                        {
                            var chosen = candidates[idx];
                            if (RenameHelper.TryRename(path, chosen, out var newPath))
                            {
                                MessageBox(hwnd, $"已重命名为:\n{Path.GetFileName(newPath)}", "AI Rename", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
                                Logger.Log($"重命名成功 | {newPath}");
                            }
                            else
                            {
                                MessageBox(hwnd, "重命名失败", "AI Rename", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
                                Logger.Log("重命名失败");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("抽取异常: " + ex);
                MessageBox(hwnd, "抽取失败: " + ex.Message, "AI Rename", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
            }
            Logger.Log("退出 | code=0");
            return 0;
        }

        // 无参数时，保留原有 HelloWorld 行为
        try
        {
            MessageBox(hwnd, "HelloWorld", "AI Rename", MB_ICONINFORMATION | MB_TOPMOST | MB_SYSTEMMODAL);
            Logger.Log("MessageBox 已显示并返回");
        }
        catch (Exception ex)
        {
            Logger.Log("MessageBox 异常: " + ex);
        }
        Logger.Log("退出 | code=0");
        return 0;
    }

    private static int GetEnvInt(string name, int @default)
    {
        try
        {
            var s = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(s)) return @default;
            if (int.TryParse(s, out var v) && v > 0) return v;
            return @default;
        }
        catch { return @default; }
    }
    private static System.Collections.Generic.List<string> BuildCandidates(string baseStem, System.Collections.Generic.List<string> top)
    {
        var list = new System.Collections.Generic.List<string>();
        // 1) 直接使用可能标题
        if (!string.IsNullOrWhiteSpace(baseStem)) list.Add(baseStem);

        // 2) 关键词拼接（空格/下划线）
        string JoinTake(int n, string sep) => string.Join(sep, top.Where(t => t.Length >= 2).Take(n));
        var k3 = JoinTake(3, " "); if (!string.IsNullOrWhiteSpace(k3)) list.Add(k3);
        var k3u = JoinTake(3, "_"); if (!string.IsNullOrWhiteSpace(k3u)) list.Add(k3u);
        var k4 = JoinTake(4, " "); if (!string.IsNullOrWhiteSpace(k4)) list.Add(k4);

        // 3) 标题 + 关键词后缀
        if (!string.IsNullOrWhiteSpace(baseStem))
        {
            var suffix = JoinTake(2, " ");
            if (!string.IsNullOrWhiteSpace(suffix)) list.Add(baseStem + " " + suffix);
        }

        // 去重与清理
        list = list
            .Select(s => RenameHelper.SanitizeFileName(s))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Take(5)
            .ToList();
        return list;
    }
}