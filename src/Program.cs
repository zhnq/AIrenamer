using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using AIRename.TextExtract;

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
                    var text = TextExtractor.Extract(path, maxChars: 2000);
                    sw.Stop();
                    proc.Refresh();
                    var wsEndMb = proc.WorkingSet64 / (1024.0 * 1024.0);
                    var msg = new StringBuilder();
                    msg.AppendLine($"文件: {Path.GetFileName(path)}");
                    msg.AppendLine($"长度: {text?.Length ?? 0} 字符");
                    msg.AppendLine($"耗时: {sw.ElapsedMilliseconds} ms");
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
}