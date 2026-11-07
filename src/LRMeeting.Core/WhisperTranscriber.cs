using System.Diagnostics;
using System.Text.Json;
namespace LRMeeting.Core;

public sealed class WhisperTranscriber : ITranscriber
{
    public async Task<IReadOnlyList<RecognizedSegment>> TranscribeAsync(AudioChunk chunk, EngineSettings settings, CancellationToken cancellationToken)
    {
        settings.ValidateWhisper();
        var prefix = chunk.FilePath + ".whisper-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(settings.WhisperExecutable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.WhisperExecutable))!
        };
        foreach (var arg in new[] { "-m", settings.WhisperModel, "-f", chunk.FilePath, "-l", settings.Language,
            "-oj", "-of", prefix, "-t", Math.Clamp(Environment.ProcessorCount / 2, 1, 8).ToString() }) start.ArgumentList.Add(arg);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new IOException("No se pudo iniciar Whisper.cpp.");
            // Drain both streams concurrently to avoid a full-pipe deadlock.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("Whisper excedió 15 minutos para un bloque; el audio sigue conservado.");
            }
            await stdout.ConfigureAwait(false);
            var error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException($"Whisper finalizó con código {process.ExitCode}: {error[..Math.Min(error.Length, 3000)]}");
            if (!File.Exists(prefix + ".json")) throw new IOException("Whisper no generó JSON. Usa una versión de whisper-cli compatible con -oj.");
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(prefix + ".json", cancellationToken).ConfigureAwait(false));
            var result = new List<RecognizedSegment>();
            foreach (var segment in json.RootElement.GetProperty("transcription").EnumerateArray())
            {
                var offsets = segment.GetProperty("offsets");
                double from = offsets.GetProperty("from").GetDouble(), to = offsets.GetProperty("to").GetDouble();
                var text = segment.GetProperty("text").GetString()?.Trim() ?? "";
                // Whisper can timestamp its padded 30-second inference window even for a short final WAV.
                // Never publish positions outside the real audio: clip the end and discard padded-only segments.
                var paddedLimit = Math.Max(30000, Math.Ceiling(chunk.DurationMs / 30000) * 30000);
                if (!double.IsFinite(from) || !double.IsFinite(to) || from < 0 || to < from || to > paddedLimit + 1500)
                    throw new InvalidDataException($"Whisper devolvió marcas no válidas: {from}–{to} ms para {chunk.DurationMs} ms de audio.");
                if (from >= chunk.DurationMs) continue;
                var end = Math.Min(to, chunk.DurationMs);
                if (text.Length != 0 && end > from) result.Add(new(from, end, text));
            }
            return result;
        }
        finally { if (File.Exists(prefix + ".json")) File.Delete(prefix + ".json"); }
    }
}
