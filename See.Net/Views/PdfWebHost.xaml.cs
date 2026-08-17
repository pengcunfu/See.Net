using System.IO;
using Microsoft.Web.WebView2.Core;
using See.Net.Core;
using See.ViewModels;

namespace See.Views;

/// <summary>
/// PDF 预览宿主：独立未映射域名 + WebResourceRequested 回吐 PDF 字节（含 Range），
/// 交给 WebView2 / Chromium 内置 PDF 查看器。不使用虚拟主机映射，避免拦截失效。
/// </summary>
public partial class PdfWebHost : WebViewHostBase
{
    public const string PdfDataHost = "see-pdf.local";

    private readonly string? _path;

    public PdfWebHost()
    {
        InitializeComponent();
    }

    public PdfWebHost(string path)
    {
        InitializeComponent();
        _path = path;
    }

    protected override void Configure(CoreWebView2 core)
    {
        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{PdfDataHost}/*", CoreWebView2WebResourceContext.All);

        if (_path is not null)
            NavigateOrPending($"https://{PdfDataHost}/document.pdf");
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_path is null
            || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(PdfDataHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            string? rangeHeader = null;
            if (e.Request.Headers.Contains("Range"))
                rangeHeader = e.Request.Headers.GetHeader("Range");

            long total = new FileInfo(_path).Length;
            var spec = RangeSpec.Parse(rangeHeader, total);
            const string mime = "application/pdf";

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
            if (DataContext is PdfContentViewModel vm)
                Dispatcher.Invoke(() => vm.Error = $"PDF 读取失败：{ex.Message}");
        }
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.WebResourceRequested -= OnWebResourceRequested;
    }

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
