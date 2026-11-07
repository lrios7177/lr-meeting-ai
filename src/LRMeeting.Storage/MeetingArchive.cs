using System.IO.Compression;
using System.Text.Json;
namespace LRMeeting.Storage;

public static class MeetingArchive
{
    // ZipArchive writes ZIP64 records when size or entry count requires them.
    // The working files are retained even if packaging fails or is cancelled.
    public static async Task<string> CreateAsync(string folder, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        folder = Path.GetFullPath(folder);
        var files = Directory.GetFiles(folder).Where(f => Path.GetExtension(f).Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(f) == "capture.jsonl").Order().ToArray();
        if (!files.Any(f => Path.GetExtension(f).Equals(".wav", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("La reunión no contiene archivos WAV.");
        var target = folder + ".lrmeeting.zip";
        var partial = target + ".partial-" + Guid.NewGuid().ToString("N");
        long total = files.Sum(f => new FileInfo(f).Length), copied = 0;
        try
        {
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous))
            {
                using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                var manifest = zip.CreateEntry("meeting.json");
                await using (var manifestStream = manifest.Open())
                    await JsonSerializer.SerializeAsync(manifestStream, new { schemaVersion = 1, createdUtc = DateTimeOffset.UtcNow,
                        sources = new[] { "A: salida de Windows", "B: micrófono" },
                        timestampMeaning = "callback arrival, approximate alignment; see capture.jsonl",
                        files = files.Select(f => new { name = Path.GetFileName(f), bytes = new FileInfo(f).Length }) }, cancellationToken: cancellationToken);
                var buffer = new byte[131072];
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.NoCompression);
                    await using var destination = entry.Open();
                    await using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        copied += read;
                        progress?.Report(total == 0 ? 1 : (double)copied / total);
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, target, overwrite: false);
            return target;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
