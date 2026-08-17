namespace See.Net.Services;

/// <summary>通过 Shell.Application COM 获取资源管理器窗口当前选中的文件。</summary>
public sealed class ExplorerSelectionService
{
    public List<string> GetSelectedFiles(IntPtr explorerHwnd)
    {
        var result = new List<string>();
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return result;

            dynamic shell = Activator.CreateInstance(shellType, true)!;
            dynamic windows = shell.Windows();
            int windowCount = windows.Count;
            for (int i = 0; i < windowCount; i++)
            {
                dynamic window = windows.Item(i);
                int hwnd = (int)window.HWND;
                if (hwnd != explorerHwnd.ToInt32()) continue;

                dynamic folder = window.Document;
                if (folder is null) break;
                dynamic items = folder.SelectedItems();
                int itemCount = items.Count;
                for (int j = 0; j < itemCount; j++)
                {
                    string? path = items.Item(j).Path as string;
                    if (!string.IsNullOrWhiteSpace(path)) result.Add(path);
                }
                break;
            }
        }
        catch
        {
            // Shell COM 访问失败（如权限/非 Explorer 窗口）时返回空列表
        }
        return result;
    }
}
