using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
        try
        {
            // 前台窗口句柄（尽量与资源管理器绑定），并设置系统模态与置顶，确保可见
            var hwnd = GetForegroundWindow();
            const int MB_ICONINFORMATION = 0x00000040;
            const int MB_TOPMOST = 0x00040000;
            const int MB_SYSTEMMODAL = 0x00001000;
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