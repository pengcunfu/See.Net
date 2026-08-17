using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using See.Net.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// 音频播放宿主：播放页与音频字节均经独立未映射域名 + WebResourceRequested 回吐。
/// 注意：WebView2 对 SetVirtualHostNameToFolderMapping 的域名不触发 WebResourceRequested，
/// 因此不能把 /data 挂在 AssetsHost（officeline.local）上。
/// </summary>
public partial class AudioWebHost : WebViewHostBase
{
    /// <summary>仅本宿主使用的音频域；不调用文件夹映射，保证可拦截。</summary>
    public const string AudioDataHost = "see-audio.local";

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
        // 故意不 MapAssets 到 AudioDataHost：映射域上 WebResourceRequested 不会触发。
        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{AudioDataHost}/*", CoreWebView2WebResourceContext.All);
        core.WebMessageReceived += OnWebMessageReceived;

        if (_path is not null)
        {
            var sizeText = FileEntry.FormatSize(_size);
            var url = $"https://{AudioDataHost}/audio-player.html"
                + $"?name={Uri.EscapeDataString(_name ?? "")}&size={Uri.EscapeDataString(sizeText)}";
            NavigateOrPending(url);
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(AudioDataHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string path = uri.AbsolutePath;
        if (path.Equals("/audio-player.html", StringComparison.OrdinalIgnoreCase))
        {
            ServePlayerPage(e);
            return;
        }

        if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase))
        {
            ServeAudioStream(e);
            return;
        }

        e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
    }

    private void ServePlayerPage(CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var htmlPath = Path.Combine(AppContext.BaseDirectory, "webassets", "audio-player.html");
            var stream = File.OpenRead(htmlPath);
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                stream, 200, "OK", "Content-Type: text/html; charset=utf-8\nCache-Control: no-cache");
        }
        catch (Exception ex)
        {
            var bytes = Encoding.UTF8.GetBytes($"<!DOCTYPE html><pre>播放页加载失败：{ex.Message}</pre>");
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                new MemoryStream(bytes), 500, "Error", "Content-Type: text/html; charset=utf-8");
        }
    }

    /// <summary>无 Range 回 200 全量；可满足 Range 回 206（限长视图流，供 seek）。</summary>
    private void ServeAudioStream(CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_path is null)
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

    /// <summary>限长视图流：底层流的 [Position, Position+length) 窗口。</summary>
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) _underlying.Dispose();
            base.Dispose(disposing);
        }
    }
}
