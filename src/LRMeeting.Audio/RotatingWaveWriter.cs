using NAudio.Wave;
namespace LRMeeting.Audio;

public sealed record WrittenAudioBlock(string FileName, long FileOffset, int Bytes, int SourceOffset);

/// <summary>Rotates frame-aligned WAV parts before RIFF's 32-bit size limit.</summary>
public sealed class RotatingWaveWriter : IDisposable
{
    private readonly string folder, channel;
    private readonly WaveFormat format;
    private readonly long maxPartBytes;
    private WaveFileWriter writer;
    private int part = 1;
    private bool disposed;
    public string FileName => $"channel-{channel}-{part:D6}.wav";
    public long Length { get; private set; }
    public int PartCount => part;
    public RotatingWaveWriter(string folder, string channel, WaveFormat format, long maxPartBytes = 1024L * 1024 * 1024)
    {
        if (format.BlockAlign <= 0) throw new ArgumentException("Formato sin alineación de muestras.", nameof(format));
        if (maxPartBytes < format.BlockAlign || maxPartBytes > uint.MaxValue - 1024L)
            throw new ArgumentOutOfRangeException(nameof(maxPartBytes));
        if (channel != "A" && channel != "B") throw new ArgumentException("Canal no válido.", nameof(channel));
        this.folder = folder; this.channel = channel; this.format = format;
        this.maxPartBytes = maxPartBytes / format.BlockAlign * format.BlockAlign;
        writer = CreateWriter();
    }
    private WaveFileWriter CreateWriter() => new(Path.Combine(folder, FileName), format);
    public IReadOnlyList<WrittenAudioBlock> Write(byte[] data, int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > data.Length || count % format.BlockAlign != 0)
            throw new ArgumentException("El bloque debe contener muestras completas.", nameof(count));
        var blocks = new List<WrittenAudioBlock>();
        int offset = 0;
        while (offset < count)
        {
            if (writer.Length == maxPartBytes)
            {
                writer.Dispose();
                part++;
                writer = CreateWriter();
            }
            int length = (int)Math.Min(count - offset, maxPartBytes - writer.Length);
            var fileOffset = writer.Length;
            writer.Write(data, offset, length);
            Length += length;
            blocks.Add(new(FileName, fileOffset, length, offset));
            offset += length;
        }
        return blocks;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        writer.Dispose();
    }
}
