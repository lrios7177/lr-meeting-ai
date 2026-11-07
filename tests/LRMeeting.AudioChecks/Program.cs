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

// Force rotation with tiny limits, reconstruct the original PCM and verify WAV headers.
var temp = Path.Combine(Path.GetTempPath(), "lr-audio-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var format = new WaveFormat(48000, 16, 2);
    byte[] original = Enumerable.Range(0, 120).Select(i => (byte)i).ToArray();
    using (var rotating = new RotatingWaveWriter(temp, "A", format, 32))
    {
        var first = rotating.Write(original[..20], 20);
        var rest = rotating.Write(original[20..], 100);
        if (rotating.PartCount != 4 || rotating.Length != 120) throw new Exception("Rotation totals incorrect.");
        if (first[0].FileOffset != 0 || rest[0].FileOffset != 20 || rest[1].SourceOffset != 12)
            throw new Exception("Block offsets incorrect.");
        try { rotating.Write(new byte[3], 3); throw new Exception("Incomplete frame accepted."); }
        catch (ArgumentException) { }
    }
    using var combined = new MemoryStream();
    foreach (var file in Directory.GetFiles(temp, "*.wav").Order())
    {
        using var wav = new WaveFileReader(file);
        if (wav.Length > 32 || wav.Length % format.BlockAlign != 0) throw new Exception("Invalid WAV size.");
        wav.CopyTo(combined);
    }
    if (!combined.ToArray().SequenceEqual(original)) throw new Exception("Audio lost or duplicated across parts.");
    Console.WriteLine("PASS WAV rotation preserves frames, data, headers and offsets");
}
finally { Directory.Delete(temp, true); }

var archiveFolder = Path.Combine(Path.GetTempPath(), "lr-archive-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(archiveFolder);
var archivePath = archiveFolder + ".lrmeeting.zip";
try
{
    var audio = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();
    File.WriteAllBytes(Path.Combine(archiveFolder, "channel-A-000001.wav"), audio);
    File.WriteAllBytes(Path.Combine(archiveFolder, "channel-B-000001.wav"), audio.Reverse().ToArray());
    File.WriteAllText(Path.Combine(archiveFolder, "capture.jsonl"), "{\"type\":\"test\"}\n");
    File.WriteAllText(Path.Combine(archiveFolder, "unrelated.txt"), "Must not enter archive");
    using (var cancellation = new CancellationTokenSource())
    {
        cancellation.Cancel();
        try { await LRMeeting.Storage.MeetingArchive.CreateAsync(archiveFolder, cancellationToken: cancellation.Token); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
    }
    if (File.Exists(archivePath) || Directory.GetFiles(Path.GetDirectoryName(archiveFolder)!, Path.GetFileName(archiveFolder) + "*.partial-*").Length != 0)
        throw new Exception("Partial archive was not cleaned up.");
    await LRMeeting.Storage.MeetingArchive.CreateAsync(archiveFolder);
    using (var archive = System.IO.Compression.ZipFile.OpenRead(archivePath))
    {
        if (archive.Entries.Count != 4 || archive.GetEntry("meeting.json") == null || archive.GetEntry("unrelated.txt") != null)
            throw new Exception("Archive contents invalid.");
        using var entry = archive.GetEntry("channel-A-000001.wav")!.Open();
        using var copy = new MemoryStream(); entry.CopyTo(copy);
        if (!copy.ToArray().SequenceEqual(audio)) throw new Exception("Archive changed the audio.");
    }
    var savedArchive = File.ReadAllBytes(archivePath);
    try { await LRMeeting.Storage.MeetingArchive.CreateAsync(archiveFolder); throw new Exception("Existing archive overwritten."); }
    catch (IOException) { }
    if (!File.ReadAllBytes(archivePath).SequenceEqual(savedArchive) || !File.ReadAllBytes(Path.Combine(archiveFolder, "channel-A-000001.wav")).SequenceEqual(audio))
        throw new Exception("Existing archive or source audio damaged.");
    Console.WriteLine("PASS Meeting archive round trip, cancellation cleanup and preservation of originals");
}
finally { Directory.Delete(archiveFolder, true); if (File.Exists(archivePath)) File.Delete(archivePath); }
