using System.Text.Json;
namespace LRMeeting.Core;

// Converts positions in WAV data into callback-derived wall-clock positions.
// Callback timing is approximate and does not correct independent device clock drift.
public sealed class CaptureTimeline
{
    private sealed record Anchor(double OffsetMs, double LengthMs, double MeetingStartMs);
    private readonly Dictionary<string, List<Anchor>> anchors = new(StringComparer.OrdinalIgnoreCase);
    public static CaptureTimeline Load(string folder, DateTimeOffset meetingStart)
    {
        var result = new CaptureTimeline();
        foreach (var log in Directory.GetFiles(folder, "capture.jsonl", SearchOption.AllDirectories))
        {
            double baseMs = 0;
            var rates = new Dictionary<string, int>();
            using var file = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                try
                {
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    var type = root.GetProperty("type").GetString();
                    if (type == "session") baseMs = (root.GetProperty("utc").GetDateTimeOffset() - meetingStart).TotalMilliseconds;
                    else if (type == "format" && root.TryGetProperty("averageBytesPerSecond", out var rate))
                        rates[root.GetProperty("channel").GetString()!] = rate.GetInt32();
                    else if (type == "block" && root.TryGetProperty("blocks", out var blocks)
                        && rates.TryGetValue(root.GetProperty("channel").GetString()!, out var bytesPerSecond) && bytesPerSecond > 0)
                    {
                        var arrival = root.GetProperty("arrivalMs").GetDouble();
                        var duration = root.GetProperty("bytes").GetInt32() * 1000d / bytesPerSecond;
                        foreach (var block in blocks.EnumerateArray())
                        {
                            var key = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(log)!, block.GetProperty("FileName").GetString()!));
                            if (!result.anchors.TryGetValue(key, out var list)) result.anchors[key] = list = [];
                            list.Add(new(block.GetProperty("FileOffset").GetInt64() * 1000d / bytesPerSecond,
                                block.GetProperty("Bytes").GetInt32() * 1000d / bytesPerSecond,
                                Math.Max(0, baseMs + arrival - duration + block.GetProperty("SourceOffset").GetInt32() * 1000d / bytesPerSecond)));
                        }
                    }
                }
                catch (JsonException) { /* A currently written final JSONL line is retried on the next scan. */ }
            }
        }
        return result;
    }
    public double Map(string file, double sourceMs)
    {
        if (!anchors.TryGetValue(Path.GetFullPath(file), out var list) || list.Count == 0) return sourceMs;
        var anchor = list.LastOrDefault(a => a.OffsetMs <= sourceMs) ?? list[0];
        return Math.Max(0, anchor.MeetingStartMs + Math.Clamp(sourceMs - anchor.OffsetMs, 0, anchor.LengthMs));
    }
    public bool HasMapping(string file) => anchors.ContainsKey(Path.GetFullPath(file));
}
