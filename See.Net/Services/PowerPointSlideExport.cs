using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace See.Services;

/// <summary>
/// 通过本机 Microsoft PowerPoint COM 将幻灯片导出为 PNG，供接近原貌的预览。
/// 必须在 STA 线程调用；未安装 PowerPoint 时 <see cref="IsAvailable"/> 为 false。
/// </summary>
public static class PowerPointSlideExport
{
    /// <summary>导出宽度（像素）；高度按幻灯片比例由 PowerPoint 自行适配时可传 0，这里固定给清晰预览。</summary>
    public const int ExportWidth = 1600;

    public const int ExportHeight = 900;

    public static bool IsAvailable()
    {
        try
        {
            return Type.GetTypeFromProgID("PowerPoint.Application") is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>在后台 STA 线程导出全部幻灯片为 PNG，返回按页序的文件路径。</summary>
    public static Task<IReadOnlyList<string>> ExportAsync(
        string presentationPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentationPath) || !File.Exists(presentationPath))
            throw new FileNotFoundException("演示文稿不存在", presentationPath);

        Directory.CreateDirectory(outputDirectory);

        var tcs = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var paths = ExportCore(presentationPath, outputDirectory, cancellationToken);
                tcs.TrySetResult(paths);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "See.PowerPointExport",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static IReadOnlyList<string> ExportCore(
        string presentationPath,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var progId = Type.GetTypeFromProgID("PowerPoint.Application")
            ?? throw new InvalidOperationException("未安装 Microsoft PowerPoint（缺少 PowerPoint.Application）。");

        dynamic? app = null;
        dynamic? presentation = null;
        var results = new List<string>();

        try
        {
            app = Activator.CreateInstance(progId)
                ?? throw new InvalidOperationException("无法创建 PowerPoint.Application。");

            // msoTrue=-1, msoFalse=0；只读打开且不显示窗口
            const int msoTrue = -1;
            const int msoFalse = 0;
            try { app.Visible = msoFalse; } catch { /* 部分版本只读 */ }
            try { app.DisplayAlerts = msoFalse; } catch { /* ignore */ }

            presentation = app.Presentations.Open(
                presentationPath,
                msoTrue,   // ReadOnly
                msoFalse,  // Untitled
                msoFalse); // WithWindow

            int count = (int)presentation.Slides.Count;
            for (int i = 1; i <= count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string outPath = Path.Combine(outputDirectory, $"slide_{i:D4}.png");
                presentation.Slides[i].Export(outPath, "PNG", ExportWidth, ExportHeight);
                if (!File.Exists(outPath))
                    throw new IOException($"幻灯片 {i} 导出失败：未生成文件。");
                results.Add(outPath);
            }

            return results;
        }
        finally
        {
            TryClose(presentation);
            TryQuit(app);
            ReleaseCom(presentation);
            ReleaseCom(app);
            presentation = null;
            app = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static void TryClose(dynamic? presentation)
    {
        if (presentation is null) return;
        try { presentation.Close(); }
        catch { /* ignore */ }
    }

    private static void TryQuit(dynamic? app)
    {
        if (app is null) return;
        try { app.Quit(); }
        catch { /* ignore */ }
    }

    private static void ReleaseCom(object? com)
    {
        if (com is null) return;
        try
        {
            if (Marshal.IsComObject(com))
                Marshal.FinalReleaseComObject(com);
        }
        catch { /* ignore */ }
    }
}
