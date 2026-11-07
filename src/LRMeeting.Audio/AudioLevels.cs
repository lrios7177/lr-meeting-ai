using NAudio.Wave;
namespace LRMeeting.Audio;
public static class AudioLevels
{
    public static float Peak(byte[] bytes, int count, WaveFormat format)
    {
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        float peak = 0;
        int stride = format.BitsPerSample / 8;
        if (stride == 0) return peak;
        for (int i = 0; i + stride <= count; i += stride)
        {
            float sample = isFloat && stride == 4 ? BitConverter.ToSingle(bytes, i) : stride switch
            {
                2 => BitConverter.ToInt16(bytes, i) / 32768f,
                3 => ((bytes[i] | bytes[i+1] << 8 | bytes[i+2] << 16) << 8 >> 8) / 8388608f,
                4 => BitConverter.ToInt32(bytes, i) / 2147483648f,
                1 => (bytes[i] - 128) / 128f,
                _ => 0
            };
            if (float.IsFinite(sample)) peak = Math.Max(peak, Math.Abs(sample));
        }
        return Math.Min(1, peak);
    }
}
