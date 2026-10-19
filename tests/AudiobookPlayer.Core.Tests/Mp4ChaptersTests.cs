using System.Buffers.Binary;
using System.Text;
using AudiobookPlayer.Core;

namespace AudiobookPlayer.Core.Tests;

public class Mp4ChaptersTests
{
    private static byte[] Box(string type, params byte[][] body)
    {
        var content = body.SelectMany(b => b).ToArray();
        var box = new byte[8 + content.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
        content.CopyTo(box, 8);
        return box;
    }

    private static byte[] Chpl(params (double Start, string Title)[] chapters)
    {
        var body = new List<byte> { 1, 0, 0, 0, 0, 0, 0, 0, (byte)chapters.Length };
        foreach (var (start, title) in chapters)
        {
            var t = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(t, (ulong)(start * 1e7));
            body.AddRange(t);
            var name = Encoding.UTF8.GetBytes(title);
            body.Add((byte)name.Length);
            body.AddRange(name);
        }
        return Box("chpl", body.ToArray());
    }

    [Fact]
    public void ReadsNeroChapters()
    {
        var file = Box("ftyp", Encoding.ASCII.GetBytes("M4B \0\0\0\0"))
            .Concat(Box("moov", Box("udta", Chpl((90, "Two"), (0, "One"), (200.5, "Three")))))
            .ToArray();
        var chapters = Mp4Chapters.Read(new MemoryStream(file));
        Assert.Equal(["One", "Two", "Three"], chapters.Select(c => c.Title));
        Assert.Equal(200.5, chapters[2].Start, 3);
    }

    [Fact]
    public void FileWithoutChaptersGivesNone()
    {
        var file = Box("ftyp", Encoding.ASCII.GetBytes("M4A \0\0\0\0")).Concat(Box("moov", Box("mvhd", new byte[100]))).ToArray();
        Assert.Empty(Mp4Chapters.Read(new MemoryStream(file)));
    }

    [Fact]
    public void GarbageGivesNone()
    {
        Assert.Empty(Mp4Chapters.Read(new MemoryStream(Encoding.ASCII.GetBytes("ID3 this is an mp3, not mp4"))));
        Assert.Empty(Mp4Chapters.Read(new MemoryStream(new byte[] { 0, 0, 0, 3, 1, 2, 3, 4 })));
    }
}
