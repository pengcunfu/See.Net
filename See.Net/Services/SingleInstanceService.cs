using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace See.Services;

/// <summary>命名互斥锁保证单例；命名管道向已运行实例转发命令行文件参数。</summary>
public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly Dispatcher _dispatcher;
    private readonly Thread _serverThread;
    private volatile bool _disposed;
    private volatile NamedPipeServerStream? _currentServer;

    /// <summary>UI 线程触发；payload 为文件路径，空串表示只展示主窗口。</summary>
    public event Action<string>? FileOpened;

    /// <summary>首个实例返回服务；后续实例转发 args 后返回 null（调用方应立即 Shutdown 退出）。</summary>
    public static SingleInstanceService? Acquire(string[] args, Dispatcher dispatcher)
    {
        string user = GetUserSuffix();
        string mutexName = $"Local\\See.Net.{user}.Singleton";
        string pipeName = $"See.Net.{user}.Instance";

        var mutex = new Mutex(initiallyOwned: false, mutexName, out _);
        bool owns;
        try
        {
            try { owns = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { owns = true; }        // 前一个实例崩溃，接管
        }
        catch
        {
            owns = true;                                            // 异常时退化为允许启动，避免打不开应用
        }

        if (!owns)
        {
            mutex.Dispose();
            Forward(args, pipeName);
            return null;
        }
        return new SingleInstanceService(mutex, pipeName, dispatcher);
    }

    private SingleInstanceService(Mutex mutex, string pipeName, Dispatcher dispatcher)
    {
        _mutex = mutex;
        _pipeName = pipeName;
        _dispatcher = dispatcher;
        _serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "See.Net.SingletonPipe" };
        _serverThread.Start();
    }

    /// <summary>当前用户唯一标识（SID，失败回退用户名），隔离跨用户 / RDP 会话。</summary>
    private static string GetUserSuffix()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            if (!string.IsNullOrEmpty(id?.User?.Value)) return id!.User!.Value;
        }
        catch { }
        return Environment.UserName;
    }

    /// <summary>尽力投递 args[0]（若为存在的文件）；对管道尚未监听做短重试。</summary>
    private static void Forward(string[] args, string pipeName)
    {
        string? path = args.Length > 0 && File.Exists(args[0]) ? Path.GetFullPath(args[0]) : "";
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                client.Connect(TimeSpan.FromMilliseconds(150));
                using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(path);
                return;
            }
            catch { Thread.Sleep(150); }
        }
    }

    private void ServerLoop()
    {
        while (!_disposed)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte);
                _currentServer = server;
                server.WaitForConnection();
                _currentServer = null;
                using var reader = new StreamReader(server, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    string payload = line.Trim();
                    try { _dispatcher.BeginInvoke(() => FileOpened?.Invoke(payload)); }
                    catch { }                                       // Dispatcher 正在关闭
                }
            }
            catch
            {
                if (_disposed) return;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _currentServer?.Dispose(); } catch { }                // 让 WaitForConnection 退出阻塞
        _serverThread.Join(1000);
        try { _mutex.ReleaseMutex(); } catch { }                    // Abandoned 状态下 Release 会抛，忽略
        _mutex.Dispose();
    }
}