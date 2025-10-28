using System;
using System.Runtime.InteropServices;

namespace AIRename.UI;

public static class ConsoleSelector
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    public static int Show(string title, string[] options)
    {
        if (options == null || options.Length == 0) return -1;
        AllocConsole();
        try
        {
            Console.Title = title;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(title);
            Console.WriteLine(new string('-', Math.Min(title.Length, 40)));
            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine($"[{i + 1}] {options[i]}");
            }
            Console.WriteLine();
            Console.Write("请选择编号 (1-{0})，或按 Esc 取消: ", options.Length);

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) return -1;
                if (key.KeyChar >= '1' && key.KeyChar <= '9')
                {
                    int idx = key.KeyChar - '1';
                    if (idx >= 0 && idx < options.Length)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"已选择: {options[idx]}");
                        return idx;
                    }
                }
            }
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("按任意键关闭窗口...");
            Console.ReadKey(intercept: true);
            FreeConsole();
        }
    }
}