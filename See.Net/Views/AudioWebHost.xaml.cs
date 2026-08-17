using System.IO;
using Microsoft.Web.WebView2.Core;
using See.Net.Core;
using See.Net.ViewModels;

namespace See.Net.Views;

/// <summary>
/// 音频播放宿主：/data 拦截回吐音频字节，实现 HTTP Range（206），
/// 供 Chromium 媒体栈 seek（未缓冲位置拖动依赖 Range 重请求）。
/// </summary>
public partial class AudioWebHost : WebViewHostBase
{
    private readonly string? _path;
    private readonly string? _name;
    private readonly long _size;

    public AudioWebHost()
    {
        InitializeComponent();
    }

    public AudioWebHost(string path, string name, long size)
    {
        InitializeComponent();
        _path = path;
        _name = name;
        _size = size;
    }

    protected override void Configure(CoreWebView2 core)
    {
        MapAssets(core);

        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{AssetsHost}/data", CoreWebView2WebResourceContext.Other);
        core.WebMessageReceived += OnWebMessageReceived;

        if (_path is not null)
        {
            var url = $"https://{AssetsHost}/audio-player.html"
                + $"?name={Uri.EscapeDataString(_name ?? "")}&size={_size}";
            NavigateOrPending(url);
        }
    }

    /// <summary>拦截 data 请求：无 Range 回 200 全量；带可满足 Range 回 206 局部（限长视图流）。</summary>
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_path is null || !e.Request.Uri.EndsWith("/data", StringComparison.Ordinal))
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }

        try
        {
            string mime = MimeFor(_path);
            string? rangeHeader = null;
            if (e.Request.Headers.Contains("Range"))
                rangeHeader = e.Request.Headers.GetHeader("Range");

            long total = new FileInfo(_path).Length;
            var spec = RangeSpec.Parse(rangeHeader, total);

            if (spec is { } s)
            {
                var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                stream.Position = s.Start;
                long length = s.End - s.Start + 1;
                var body = new SubStream(stream, length);
                string headers =
                    $"Content-Type: {mime}\nContent-Length: {length}\nAccept-Ranges: bytes\n" +
                    $"Content-Range: bytes {s.Start}-{s.End}/{total}";
                e.Response = SharedEnvironment.CreateWebResourceResponse(body, 206, "Partial Content", headers);
            }
            else
            {
                var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                e.Response = SharedEnvironment.CreateWebResourceResponse(
                    stream, 200, "OK",
                    $"Content-Type: {mime}\nContent-Length: {total}\nAccept-Ranges: bytes");
            }
        }
        catch (Exception ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, ex.Message, "");
        }
    }

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".flac" => "audio/flac",
        ".ogg" or ".oga" or ".opus" => "audio/ogg",
        ".m4a" or ".aac" => "audio/mp4",
        ".wma" => "audio/x-ms-wma",
        ".aif" or ".aiff" => "audio/aiff",
        _ => "application/octet-stream",
    };

    /// <summary>接收播放页 postMessage 的错误上报（编解码不支持 / 拉取失败）。</summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var vm = DataContext as AudioContentViewModel;
        if (vm is null) return;
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (json?.Contains("\"type\":\"error\"") == true)
                Dispatcher.Invoke(() => vm.Error = ExtractMessage(json));
        }
        catch
        {
            // 非字符串消息忽略
        }
    }

    private static string ExtractMessage(string json)
    {
        int idx = json.IndexOf("\"message\":\"", StringComparison.Ordinal);
        if (idx < 0) return json;
        int start = idx + 11;
        int end = json.IndexOf('"', start);
        return end > start ? json[start..end] : json;
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.WebResourceRequested -= OnWebResourceRequested;
        core.WebMessageReceived -= OnWebMessageReceived;
    }

    /// <summary>限长视图流：底层流的 [Position, Position+length) 窗口，Content-Length 与实际可读字节严格一致。</summary>
    private sealed class SubStream : Stream
    {
        private readonly Stream _underlying;
        private readonly long _length;
        private long _remaining;

        public SubStream(Stream underlying, long length)
        {
            _underlying = underlying;
            _length = length;
            _remaining = length;
        }

        public override bool CanRead => _underlying.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _length - _remaining;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            int toRead = (int)Math.Min(count, _remaining);
            int read = _underlying.Read(buffer, offset, toRead);
            _remaining -= read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
