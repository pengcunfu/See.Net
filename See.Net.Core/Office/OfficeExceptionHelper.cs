using System.IO;

namespace See.Net.Core.Office;

/// <summary>Office 文档操作的异常处理辅助类。</summary>
internal static class OfficeExceptionHelper
{
    /// <summary>验证文件是否存在和可读。</summary>
    public static void ValidateFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("文件路径不能为空", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"文件不存在: {path}");

        try
        {
            // 尝试获取文件属性来验证可访问性
            var _ = File.GetAttributes(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"无权访问文件: {path}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"无法读取文件: {path}", ex);
        }
    }

    /// <summary>包装 Office 操作，提供统一的异常处理。</summary>
    public static T WrapOfficeOperation<T>(string fileType, string path, Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (FileFormatException ex)
        {
            throw new InvalidDataException($"{fileType}文档格式损坏: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"读取{fileType}文档时发生IO错误: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"无权访问{fileType}文档: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidDataException && ex is not IOException && ex is not UnauthorizedAccessException)
        {
            throw new InvalidDataException($"读取{fileType}文档失败: {ex.Message}", ex);
        }
    }
}
