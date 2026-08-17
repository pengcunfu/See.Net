namespace See.Net.Core;

/// <summary>
/// 十六进制文档模型：以「原文件分段 + 内存插入分段」的方式提供虚拟字节流，
/// 支持覆盖、插入、删除与保存，超大文件也不会整文件载入内存。
/// </summary>
public sealed class HexDocument : IDisposable
{
    private sealed class Segment
    {
        public long OriginalStart;
        public long OriginalLength;
        public byte[]? Data; // 非 null 表示内存插入段
        public bool IsInserted => Data is not null;
    }

    private FileStream _stream;
    private readonly object _lock = new();
    private readonly List<Segment> _segments = [];
    private long[] _cumStarts = [];
    private long _length;

    private HexDocument(FileStream stream)
    {
        _stream = stream;
        _segments.Add(new Segment { OriginalStart = 0, OriginalLength = stream.Length });
        RebuildIndex();
    }

    public static HexDocument Open(string path) =>
        new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));

    public string? SourcePath => _stream.Name;

    public long Length
    {
        get { lock (_lock) return _length; }
    }

    public bool HasChanges
    {
        get
        {
            lock (_lock)
            {
                foreach (var s in _segments)
                {
                    if (s.IsInserted) return true;
                }
                return false;
            }
        }
    }

    /// <summary>指定偏移处的字节是否来自编辑（新增/覆盖），用于高亮。</summary>
    public bool IsEdited(long offset)
    {
        lock (_lock)
        {
            if (offset < 0 || offset >= _length) return false;
            var (seg, _, _) = Locate(offset);
            return seg.IsInserted;
        }
    }

    public byte ReadByte(long offset)
    {
        var data = ReadBytes(offset, 1);
        return data.Length == 0 ? (byte)0 : data[0];
    }

    public byte[] ReadBytes(long offset, int count)
    {
        lock (_lock)
        {
            if (offset < 0 || offset >= _length || count <= 0) return [];
            count = (int)Math.Min(count, _length - offset);
            var result = new byte[count];
            long pos = offset;
            int done = 0;
            while (done < count)
            {
                var (seg, _, within) = Locate(pos);
                if (seg.IsInserted)
                {
                    int take = (int)Math.Min(seg.Data!.Length - within, count - done);
                    Array.Copy(seg.Data, within, result, done, take);
                    pos += take;
                    done += take;
                }
                else
                {
                    int take = (int)Math.Min(seg.OriginalLength - within, count - done);
                    if (take <= 0) { pos++; continue; }
                    _stream.Position = seg.OriginalStart + within;
                    ReadExact(_stream, result, done, take);
                    pos += take;
                    done += take;
                }
            }
            return result;
        }
    }

    /// <summary>覆盖写入，效果等同先删除后插入。</summary>
    public void WriteBytes(long offset, byte[] data)
    {
        if (data is null || data.Length == 0) return;
        lock (_lock)
        {
            if (offset < 0 || offset + data.Length > _length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            DeleteRangeCore(offset, data.Length);
            InsertBytesCore(offset, data);
        }
    }

    public void WriteByte(long offset, byte value) => WriteBytes(offset, [value]);

    /// <summary>在指定偏移处插入字节（允许 offset == Length 表示追加）。</summary>
    public void InsertBytes(long offset, byte[] data)
    {
        if (data is null || data.Length == 0) return;
        lock (_lock)
        {
            if (offset < 0 || offset > _length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            InsertBytesCore(offset, data);
        }
    }

    /// <summary>删除 [offset, offset+count) 范围。</summary>
    public void DeleteRange(long offset, long count)
    {
        lock (_lock)
        {
            if (offset < 0 || offset >= _length || count <= 0) return;
            count = Math.Min(count, _length - offset);
            DeleteRangeCore(offset, count);
        }
    }

    /// <summary>
    /// 查找字节模式，返回首次命中的虚拟偏移；未找到返回 -1。
    /// 分块读取，支持超大文件。
    /// </summary>
    public long Find(byte[] pattern, long startOffset = 0)
    {
        lock (_lock)
        {
            if (pattern is null || pattern.Length == 0 || startOffset < 0 || startOffset >= _length)
                return -1;

            int m = pattern.Length;
            const int Chunk = 1 << 20;
            long pos = startOffset;
            while (pos < _length)
            {
                int take = (int)Math.Min(Chunk + m - 1, _length - pos);
                var buf = ReadBytesCore(pos, take);
                int limit = buf.Length - m;
                for (int i = 0; i <= limit; i++)
                {
                    bool ok = true;
                    for (int k = 0; k < m; k++)
                    {
                        if (buf[i + k] != pattern[k]) { ok = false; break; }
                    }
                    if (ok) return pos + i;
                }
                if (take < Chunk + m - 1) break;
                pos += Chunk;
            }
            return -1;
        }
    }

    /// <summary>把当前文档内容保存到指定路径（临时文件 + 原子替换）。</summary>
    public void Save(string path)
    {
        lock (_lock)
        {
            string fullPath = Path.GetFullPath(path);
            bool sameFile = string.Equals(fullPath, Path.GetFullPath(_stream.Name), StringComparison.OrdinalIgnoreCase);
            if (sameFile && !HasChanges) return;

            string dir = Path.GetDirectoryName(fullPath) ?? ".";
            string temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    foreach (var seg in _segments)
                    {
                        if (seg.IsInserted)
                        {
                            fs.Write(seg.Data!);
                        }
                        else
                        {
                            CopyRange(fs, seg.OriginalStart, seg.OriginalLength);
                        }
                    }
                    fs.Flush(flushToDisk: true);
                }
                if (sameFile)
                {
                    // 替换前必须释放源文件句柄
                    try
                    {
                        _stream.Dispose();
                        File.Move(temp, fullPath, overwrite: true);
                    }
                    catch
                    {
                        try
                        {
                            _stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            _segments.Clear();
                            _segments.Add(new Segment { OriginalStart = 0, OriginalLength = _stream.Length });
                            RebuildIndex();
                        }
                        catch { /* 恢复失败时保持原状态 */ }
                        throw;
                    }
                    _stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    _segments.Clear();
                    _segments.Add(new Segment { OriginalStart = 0, OriginalLength = _stream.Length });
                    RebuildIndex();
                }
                else
                {
                    File.Move(temp, fullPath, overwrite: true);
                }
            }
            catch
            {
                try { File.Delete(temp); } catch { /* 忽略清理失败 */ }
                throw;
            }
        }
    }

    public void Dispose() => _stream.Dispose();

    private void RebuildIndex()
    {
        // 清理空分段，避免读取循环无法前进
        _segments.RemoveAll(s => (s.IsInserted && s.Data!.Length == 0) || (!s.IsInserted && s.OriginalLength == 0));
        _cumStarts = new long[_segments.Count];
        long pos = 0;
        for (int i = 0; i < _segments.Count; i++)
        {
            _cumStarts[i] = pos;
            var seg = _segments[i];
            pos += seg.IsInserted ? seg.Data!.Length : seg.OriginalLength;
        }
        _length = pos;
    }

    private (Segment Seg, int Index, long Within) Locate(long offset)
    {
        int idx = Array.BinarySearch(_cumStarts, offset);
        if (idx < 0) idx = ~idx - 1;
        if (idx < 0) idx = 0;
        if (idx >= _segments.Count) idx = _segments.Count - 1;
        var seg = _segments[idx];
        return (seg, idx, offset - _cumStarts[idx]);
    }

    private byte[] ReadBytesCore(long offset, int count)
    {
        if (offset < 0 || offset >= _length || count <= 0) return [];
        count = (int)Math.Min(count, _length - offset);
        var result = new byte[count];
        long pos = offset;
        int done = 0;
        while (done < count)
        {
            var (seg, _, within) = Locate(pos);
            if (seg.IsInserted)
            {
                int take = (int)Math.Min(seg.Data!.Length - within, count - done);
                Array.Copy(seg.Data, within, result, done, take);
                pos += take;
                done += take;
            }
            else
            {
                int take = (int)Math.Min(seg.OriginalLength - within, count - done);
                if (take <= 0) { pos++; continue; }
                _stream.Position = seg.OriginalStart + within;
                ReadExact(_stream, result, done, take);
                pos += take;
                done += take;
            }
        }
        return result;
    }

    private void InsertBytesCore(long offset, byte[] data)
    {
        if (offset == _length)
        {
            _segments.Add(new Segment { Data = (byte[])data.Clone() });
            RebuildIndex();
            return;
        }

        var (seg, idx, within) = Locate(offset);
        if (seg.IsInserted)
        {
            var list = new byte[seg.Data!.Length + data.Length];
            Array.Copy(seg.Data, 0, list, 0, within);
            Array.Copy(data, 0, list, within, data.Length);
            Array.Copy(seg.Data, within, list, within + data.Length, seg.Data.Length - within);
            seg.Data = list;
        }
        else
        {
            long before = within;
            long after = seg.OriginalLength - within;
            var inserted = new Segment { Data = (byte[])data.Clone() };
            var tail = new Segment { OriginalStart = seg.OriginalStart + within, OriginalLength = after };
            seg.OriginalLength = before;
            _segments.Insert(idx + 1, inserted);
            if (after > 0) _segments.Insert(idx + 2, tail);
        }
        RebuildIndex();
    }

    private void DeleteRangeCore(long offset, long count)
    {
        long pos = offset;
        long remaining = count;
        while (remaining > 0)
        {
            // 每次迭代前重建索引，确保 Locate 基于最新分段布局
            RebuildIndex();
            var (seg, idx, within) = Locate(pos);
            if (seg.IsInserted)
            {
                int take = (int)Math.Min(seg.Data!.Length - within, remaining);
                if (take <= 0) { pos++; continue; }
                if (take == seg.Data.Length)
                {
                    _segments.RemoveAt(idx);
                }
                else
                {
                    var list = new byte[seg.Data.Length - take];
                    Array.Copy(seg.Data, 0, list, 0, within);
                    Array.Copy(seg.Data, within + take, list, within, seg.Data.Length - within - take);
                    seg.Data = list;
                }
                remaining -= take;
            }
            else
            {
                long take = Math.Min(seg.OriginalLength - within, remaining);
                long before = within;
                long after = seg.OriginalLength - within - take;
                if (before > 0 && after > 0)
                {
                    var tail = new Segment { OriginalStart = seg.OriginalStart + within + take, OriginalLength = after };
                    seg.OriginalLength = before;
                    _segments.Insert(idx + 1, tail);
                }
                else if (before > 0)
                {
                    seg.OriginalLength = before;
                }
                else if (after > 0)
                {
                    seg.OriginalStart += take;
                    seg.OriginalLength = after;
                }
                else
                {
                    _segments.RemoveAt(idx);
                }
                remaining -= take;
            }
        }
        RebuildIndex();
    }

    private void CopyRange(FileStream target, long start, long length)
    {
        const int BufferSize = 1 << 20;
        var buffer = new byte[BufferSize];
        _stream.Position = start;
        long left = length;
        while (left > 0)
        {
            int take = (int)Math.Min(BufferSize, left);
            int read = ReadExact(_stream, buffer, 0, take);
            if (read == 0) break;
            target.Write(buffer, 0, read);
            left -= read;
        }
    }

    private static int ReadExact(Stream stream, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, offset + total, count - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
