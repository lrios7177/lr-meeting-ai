using System.Security.Cryptography;
using System.Text;
namespace LRMeeting.Core;

public sealed class MeetingPipeline(IMeetingStore store, IAudioPreparer preparer, ITranscriber transcriber)
{
    public event Action<TranscriptSegment>? SegmentAdded;
    public async Task TranscribeAsync(Meeting meeting, IReadOnlyList<AudioSource> sources, EngineSettings settings, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        settings.ValidateWhisper();
        var timeline = CaptureTimeline.Load(meeting.Folder, meeting.CreatedUtc);
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(meeting.Folder, source.FilePath).Replace('\\', '/');
            if (store.IsChunkDone(meeting.Id, relative, -1)) continue;
            var chunks = await preparer.PrepareAsync(source.FilePath, Path.Combine(meeting.Folder, ".processing"), cancellationToken).ConfigureAwait(false);
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (store.IsChunkDone(meeting.Id, relative, chunk.Index)) continue;
                progress?.Report($"Whisper · canal {source.Channel} · {Path.GetFileName(source.FilePath)} · bloque {chunk.Index + 1}");
                var recognized = chunk.HasAudio ? await transcriber.TranscribeAsync(chunk, settings, cancellationToken).ConfigureAwait(false) : [];
                var segments = new List<TranscriptSegment>();
                foreach (var (part, index) in recognized.Select((s, i) => (s, i)))
                {
                    var key = meeting.Id + "|" + relative + "|" + chunk.Index + "|" + index;
                    var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
                    var from = timeline.Map(source.FilePath, chunk.OffsetMs + part.StartMs);
                    var to = Math.Max(from, timeline.Map(source.FilePath, chunk.OffsetMs + part.EndMs));
                    segments.Add(new(id, meeting.Id, source.Channel, relative, from, to, part.Text.Trim()));
                }
                // Unique chunk checkpoint and inserts commit together: retries cannot duplicate text.
                store.CommitChunk(meeting.Id, relative, chunk.Index, segments);
                foreach (var segment in segments) SegmentAdded?.Invoke(segment);
            }
            store.CommitChunk(meeting.Id, relative, -1, []);
        }
    }
    public static IReadOnlyList<AudioSource> DiscoverSources(string folder)
    {
        var result = new List<AudioSource>();
        foreach (var directory in Directory.EnumerateDirectories(folder).Where(p => Path.GetFileName(p).StartsWith("take-", StringComparison.Ordinal)).Prepend(folder))
        {
            foreach (var channel in new[] { "A", "B" })
            {
                var files = Directory.GetFiles(directory, $"channel-{channel}*.wav").Order(StringComparer.Ordinal).ToList();
                if (File.Exists(Path.Combine(directory, ".recording")) && files.Count != 0) files.RemoveAt(files.Count - 1);
                result.AddRange(files.Select(file => new AudioSource(file, channel)));
            }
            result.AddRange(Directory.GetFiles(directory, "imported.*").Where(f => new[] { ".wav", ".mp3", ".flac", ".m4a" }.Contains(Path.GetExtension(f).ToLowerInvariant())).Select(f => new AudioSource(f, "Importado")));
        }
        return result.OrderBy(s => s.FilePath, StringComparer.Ordinal).ToArray();
    }
}
