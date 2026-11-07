using LRMeeting.Audio;
using NAudio.Wave;
static void Check(string name, byte[] data, WaveFormat format, float expected)
{
    var actual = AudioLevels.Peak(data, data.Length, format);
    if (Math.Abs(actual - expected) > 0.0001f) throw new Exception($"{name}: {actual} != {expected}");
    Console.WriteLine($"PASS {name}");
}
Check("Silencio PCM16", new byte[64], new WaveFormat(48000, 16, 1), 0);
Check("PCM16 negativo máximo", BitConverter.GetBytes(short.MinValue), new WaveFormat(48000, 16, 1), 1);
Check("PCM24 signo", new byte[] {0,0,128}, new WaveFormat(48000, 24, 1), 1);
Check("PCM8 silencio", new byte[] {128}, new WaveFormat(48000, 8, 1), 0);
Check("Float", BitConverter.GetBytes(-0.5f), WaveFormat.CreateIeeeFloatWaveFormat(48000, 1), 0.5f);
Check("Float inválido", BitConverter.GetBytes(float.NaN), WaveFormat.CreateIeeeFloatWaveFormat(48000, 1), 0);
Check("Float extensible", BitConverter.GetBytes(0.75f), new WaveFormatExtensible(48000, 32, 2), 0.75f);
Check("Muestra incompleta", new byte[] {255}, new WaveFormat(48000, 16, 1), 0);
