using System.Runtime.InteropServices;

namespace See.Net.Services;

/// <summary>全局低级键盘钩子（WH_KEYBOARD_LL）。回调运行在安装线程的消息循环（WPF UI 线程）。</summary>
public sealed class KeyboardHook : IDisposable
{
    public const int VK_SPACE = 0x20;
    public const int VK_ESCAPE = 0x1B;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;

    /// <summary>键按下时回调；返回 true 表示消费该按键（不再传递给系统）。</summary>
    public Func<int, bool>? KeyPressed;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public KeyboardHook() => _proc = HookCallback;

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);
            if (KeyPressed?.Invoke(vk) == true)
            {
                return (IntPtr)1; // 吞掉按键，不让系统/其他窗口收到
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
