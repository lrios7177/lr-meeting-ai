using System.Text.Json;
namespace LRMeeting.Core;

public sealed record EngineSettings
{
    public string WhisperExecutable { get; init; } = "";
    public string WhisperModel { get; init; } = "";
    public string Language { get; init; } = "es";
    public string OllamaUrl { get; init; } = "http://127.0.0.1:11434";
    public string OllamaModel { get; init; } = "qwen3:4b";
    public bool AutoProcess { get; init; } = true;
    public void ValidateWhisper()
    {
        if (!File.Exists(WhisperExecutable)) throw new FileNotFoundException("Selecciona whisper-cli.exe en Ajustes.", WhisperExecutable);
        if (!File.Exists(WhisperModel)) throw new FileNotFoundException("Selecciona un modelo local de Whisper (.bin) en Ajustes.", WhisperModel);
        if (Language is not ("es" or "en")) throw new ArgumentException("El idioma debe ser es o en.");
    }
}
public sealed record Meeting(string Id, string Folder, string Title, DateTimeOffset CreatedUtc, double DurationMs = 0, string State = "Grabada", string? Error = null);
public sealed record AudioSource(string FilePath, string Channel);
public sealed record AudioChunk(string FilePath, int Index, double OffsetMs, double DurationMs, bool HasAudio = true);
public sealed record RecognizedSegment(double StartMs, double EndMs, string Text);
public sealed record TranscriptSegment(string Id, string MeetingId, string Channel, string SourceFile, double StartMs, double EndMs, string OriginalText, string? CorrectedText = null)
{
    public string Text => CorrectedText ?? OriginalText;
}
public sealed record ReportPoint(string Title, string Detail, string[] Evidence);
public sealed record ActionItem(string Task, string? Owner, string? DueDate, string[] Evidence);
public sealed record ExecutiveReport(string Title, string Summary, ReportPoint[] Topics, ReportPoint[] Decisions, ActionItem[] Actions, ReportPoint[] OpenIssues, ReportPoint[] Risks)
{
    public DateTimeOffset GeneratedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; init; } = "Borrador IA · requiere revisión";
    public string Coverage { get; init; } = "";
}
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
public interface IAudioPreparer
{
    Task<IReadOnlyList<AudioChunk>> PrepareAsync(string sourceFile, string workingFolder, CancellationToken cancellationToken);
}
public interface ITranscriber
{
    Task<IReadOnlyList<RecognizedSegment>> TranscribeAsync(AudioChunk chunk, EngineSettings settings, CancellationToken cancellationToken);
}
public interface IMeetingStore
{
    void SaveMeeting(Meeting meeting);
    IReadOnlyList<Meeting> ListMeetings();
    IReadOnlyList<TranscriptSegment> GetSegments(string meetingId);
    bool IsChunkDone(string meetingId, string source, int index);
    void CommitChunk(string meetingId, string source, int index, IReadOnlyList<TranscriptSegment> segments);
    void SaveCorrections(string meetingId, IReadOnlyDictionary<string, string> corrections);
    void SaveReport(string meetingId, ExecutiveReport report);
    ExecutiveReport? GetReport(string meetingId);
}
