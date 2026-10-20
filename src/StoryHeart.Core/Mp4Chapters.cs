using System.Buffers.Binary;
using System.Text;

namespace StoryHeart.Core;

public sealed record Chapter(double Start, string Title);

/// <summary>
/// Reads chapter markers from MP4 audiobooks (.m4b, .m4a). Supports the Nero "chpl" box
/// and QuickTime text-track chapters, the two formats audiobook tools write.
/// </summary>
public static class Mp4Chapters
{
    private const long MaxMoovSize = 64L * 1024 * 1024;

    public static IReadOnlyList<Chapter> Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read(stream);
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    public static IReadOnlyList<Chapter> Read(Stream file)
    {
        try
        {
            long off = 0, size = file.Length;
            var head = new byte[16];
            while (off + 8 <= size)
            {
                file.Position = off;
                int got = ReadFully(file, head);
                if (got < 8) break;
                long boxSize = BinaryPrimitives.ReadUInt32BigEndian(head);
                var type = FourCC(head, 4);
                int hdr = 8;
                if (boxSize == 1 && got >= 16) { boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(8)); hdr = 16; }
                else if (boxSize == 0) boxSize = size - off;
                if (boxSize < hdr) break;

                if (type == "moov")
                {
                    if (boxSize > MaxMoovSize) return [];
                    var moov = new byte[boxSize];
                    file.Position = off;
                    if (ReadFully(file, moov) < boxSize) return [];
                    var chpl = SearchChpl(moov, hdr, moov.Length);
                    if (chpl.Count > 0) return chpl;
                    return ReadQuickTimeChapters(file, moov, hdr, moov.Length);
                }
                off += boxSize;
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException or IndexOutOfRangeException or OverflowException) { }
        return [];
    }

    // ---------- Nero "chpl" chapters (moov/udta/chpl) ----------

    private static List<Chapter> SearchChpl(byte[] d, int start, int end)
    {
        foreach (var b in Children(d, start, end))
        {
            if (b.Type == "chpl") return ParseChpl(d, b.Off + b.Hdr, b.Off + b.Size);
            if (b.Type == "udta")
            {
                var r = SearchChpl(d, b.Off + b.Hdr, b.Off + b.Size);
                if (r.Count > 0) return r;
            }
        }
        return [];
    }

    private static List<Chapter> ParseChpl(byte[] d, int o, int end)
    {
        int version = d[o];
        o += 4;
        if (version != 0) o += 4;
        int n = d[o++];
        var outList = new List<Chapter>(n);
        for (int i = 0; i < n && o + 9 <= end; i++)
        {
            double start = BinaryPrimitives.ReadUInt64BigEndian(d.AsSpan(o)) / 1e7;
            o += 8;
            int len = d[o++];
            var title = Encoding.UTF8.GetString(d, o, Math.Min(len, end - o)).Trim();
            o += len;
            outList.Add(new Chapter(start, title.Length > 0 ? title : $"Chapter {i + 1}"));
        }
        return outList.OrderBy(c => c.Start).ToList();
    }

    // ---------- QuickTime text-track chapters (tref/chap → text track samples) ----------

    private static IReadOnlyList<Chapter> ReadQuickTimeChapters(Stream file, byte[] d, int start, int end)
    {
        var traks = Children(d, start, end)
            .Where(b => b.Type == "trak")
            .Select(t => (Trak: t, Kids: Children(d, t.Off + t.Hdr, t.Off + t.Size)))
            .ToList();
        if (traks.Count < 2) return [];

        var chapIds = new List<uint>();
        foreach (var t in traks)
        {
            var tref = Find(t.Kids, "tref");
            if (tref is null) continue;
            var chap = Find(Children(d, tref.Off + tref.Hdr, tref.Off + tref.Size), "chap");
            if (chap is null) continue;
            int c = chap.Off + chap.Hdr, n = (chap.Size - chap.Hdr) >> 2;
            for (int i = 0; i < n; i++) chapIds.Add(BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + i * 4)));
            if (chapIds.Count > 0) break;
        }
        if (chapIds.Count == 0) return [];

        var target = traks.FirstOrDefault(t => Find(t.Kids, "tkhd") is { } tkhd && TrackId(d, tkhd) == chapIds[0]);
        var mdia = target.Kids is null ? null : Find(target.Kids, "mdia");
        if (mdia is null) return [];
        var mdiaKids = Children(d, mdia.Off + mdia.Hdr, mdia.Off + mdia.Size);
        var mdhd = Find(mdiaKids, "mdhd");
        var minf = Find(mdiaKids, "minf");
        if (mdhd is null || minf is null) return [];
        double scale = Timescale(d, mdhd);
        if (scale <= 0) scale = 1000;
        var stbl = Find(Children(d, minf.Off + minf.Hdr, minf.Off + minf.Size), "stbl");
        if (stbl is null) return [];
        var stblKids = Children(d, stbl.Off + stbl.Hdr, stbl.Off + stbl.Size);
        var stts = Find(stblKids, "stts");
        var stsz = Find(stblKids, "stsz");
        var stsc = Find(stblKids, "stsc");
        if (stts is null || stsz is null || stsc is null) return [];

        var durations = ReadPairs(d, stts, 8);
        var (uniform, sizes, count) = ReadSizes(d, stsz);
        var perChunk = ReadPairs(d, stsc, 12);
        var chunkOffsets = ReadChunkOffsets(d, stblKids);
        if (chunkOffsets.Count == 0 || count == 0) return [];

        // Lay out where each sample sits in the file.
        var samples = new List<(long Off, uint Size)>();
        for (int ci = 0; ci < chunkOffsets.Count && samples.Count < count; ci++)
        {
            uint chunkNum = (uint)ci + 1;
            uint spc = perChunk.Count > 0 ? perChunk[0].B : 0;
            foreach (var (first, n) in perChunk) { if (first <= chunkNum) spc = n; else break; }
            long off = chunkOffsets[ci];
            for (uint s = 0; s < spc && samples.Count < count; s++)
            {
                uint sz = uniform != 0 ? uniform : sizes[samples.Count];
                samples.Add((off, sz));
                off += sz;
            }
        }
        if (samples.Count == 0 || samples.Count > 1000) return [];

        var starts = new List<long>(samples.Count);
        long acc = 0;
        foreach (var (n, delta) in durations)
            for (uint i = 0; i < n && starts.Count < samples.Count; i++) { starts.Add(acc); acc += delta; }

        var result = new List<Chapter>();
        for (int i = 0; i < samples.Count; i++)
        {
            var (off, size) = samples[i];
            if (size < 2 || size > 2000) continue;
            var buf = new byte[size];
            file.Position = off;
            if (ReadFully(file, buf) < size) continue;
            int len = BinaryPrimitives.ReadUInt16BigEndian(buf);
            var text = len > 0 && len <= size - 2
                ? Encoding.UTF8.GetString(buf, 2, len)
                : Encoding.UTF8.GetString(buf).TrimEnd('\0');
            var title = text.Trim();
            if (title.Length > 0) result.Add(new Chapter((i < starts.Count ? starts[i] : 0) / scale, title));
        }
        return result;
    }

    // ---------- box helpers ----------

    private sealed record Box(string Type, int Off, int Hdr, int Size);

    private static List<Box> Children(byte[] d, int start, int end)
    {
        var list = new List<Box>();
        int o = start;
        while (o + 8 <= end)
        {
            long s = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
            var t = FourCC(d, o + 4);
            int hdr = 8;
            if (s == 1)
            {
                if (o + 16 > end) break;
                s = (long)BinaryPrimitives.ReadUInt64BigEndian(d.AsSpan(o + 8));
                hdr = 16;
            }
            else if (s == 0) s = end - o;
            if (s < hdr || o + s > end) break;
            list.Add(new Box(t, o, hdr, (int)s));
            o += (int)s;
        }
        return list;
    }

    private static Box? Find(List<Box> boxes, string type) => boxes.FirstOrDefault(b => b.Type == type);

    private static uint TrackId(byte[] d, Box tkhd)
    {
        int c = tkhd.Off + tkhd.Hdr;
        return BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(d[c] == 1 ? c + 20 : c + 12));
    }

    private static uint Timescale(byte[] d, Box mdhd)
    {
        int c = mdhd.Off + mdhd.Hdr;
        return BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(d[c] == 1 ? c + 20 : c + 12));
    }

    /// <summary>Reads the first two 32-bit fields of each entry in an stts (8-byte) or stsc (12-byte) table.</summary>
    private static List<(uint A, uint B)> ReadPairs(byte[] d, Box b, int entrySize)
    {
        int c = b.Off + b.Hdr, end = b.Off + b.Size;
        uint n = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + 4));
        var list = new List<(uint, uint)>();
        int o = c + 8;
        for (uint i = 0; i < n && o + entrySize <= end; i++, o += entrySize)
            list.Add((BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o)), BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o + 4))));
        return list;
    }

    private static (uint Uniform, List<uint> Sizes, uint Count) ReadSizes(byte[] d, Box b)
    {
        int c = b.Off + b.Hdr, end = b.Off + b.Size;
        uint uniform = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + 4));
        uint n = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + 8));
        var sizes = new List<uint>();
        if (uniform != 0) return (uniform, sizes, n);
        int o = c + 12;
        for (uint i = 0; i < n && o + 4 <= end; i++, o += 4) sizes.Add(BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o)));
        return (0, sizes, (uint)sizes.Count);
    }

    private static List<long> ReadChunkOffsets(byte[] d, List<Box> stblKids)
    {
        var list = new List<long>();
        if (Find(stblKids, "co64") is { } co64)
        {
            int c = co64.Off + co64.Hdr, end = co64.Off + co64.Size, o = c + 8;
            uint n = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + 4));
            for (uint i = 0; i < n && o + 8 <= end; i++, o += 8) list.Add((long)BinaryPrimitives.ReadUInt64BigEndian(d.AsSpan(o)));
        }
        else if (Find(stblKids, "stco") is { } stco)
        {
            int c = stco.Off + stco.Hdr, end = stco.Off + stco.Size, o = c + 8;
            uint n = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(c + 4));
            for (uint i = 0; i < n && o + 4 <= end; i++, o += 4) list.Add(BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o)));
        }
        return list;
    }

    private static string FourCC(byte[] d, int o) => Encoding.Latin1.GetString(d, o, 4);

    private static int ReadFully(Stream s, byte[] buf)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int n = s.Read(buf, total, buf.Length - total);
            if (n == 0) break;
            total += n;
        }
        return total;
    }
}
