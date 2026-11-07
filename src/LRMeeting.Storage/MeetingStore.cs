using System.Text.Json;
using LRMeeting.Core;
using Microsoft.Data.Sqlite;
namespace LRMeeting.Storage;

public sealed class MeetingStore : IMeetingStore
{
    private readonly string connectionString;
    public MeetingStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, DefaultTimeout = 10 }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS Meetings(Id TEXT PRIMARY KEY,Folder TEXT NOT NULL,Title TEXT NOT NULL,CreatedUtc TEXT NOT NULL,DurationMs REAL NOT NULL,State TEXT NOT NULL,Error TEXT);
        CREATE TABLE IF NOT EXISTS Segments(Id TEXT PRIMARY KEY,MeetingId TEXT NOT NULL REFERENCES Meetings(Id),Channel TEXT NOT NULL,SourceFile TEXT NOT NULL,StartMs REAL NOT NULL,EndMs REAL NOT NULL,OriginalText TEXT NOT NULL,CorrectedText TEXT);
        CREATE INDEX IF NOT EXISTS IX_Segments_Meeting ON Segments(MeetingId,StartMs);
        CREATE TABLE IF NOT EXISTS Chunks(MeetingId TEXT NOT NULL REFERENCES Meetings(Id),Source TEXT NOT NULL,ChunkIndex INTEGER NOT NULL,PRIMARY KEY(MeetingId,Source,ChunkIndex));
        CREATE TABLE IF NOT EXISTS Revisions(Id INTEGER PRIMARY KEY,MeetingId TEXT NOT NULL REFERENCES Meetings(Id),SegmentId TEXT NOT NULL REFERENCES Segments(Id),Text TEXT NOT NULL,CreatedUtc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Reports(Id INTEGER PRIMARY KEY,MeetingId TEXT NOT NULL REFERENCES Meetings(Id),CreatedUtc TEXT NOT NULL,Json TEXT NOT NULL);
        PRAGMA user_version=1;
        """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(connectionString); connection.Open(); return connection; }
    private static void Params(SqliteCommand command, params (string Name, object? Value)[] values)
    { foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value); }
    public void SaveMeeting(Meeting meeting)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO Meetings VALUES($id,$folder,$title,$created,$duration,$state,$error)
        ON CONFLICT(Id) DO UPDATE SET Title=excluded.Title,DurationMs=excluded.DurationMs,State=excluded.State,Error=excluded.Error;
        """;
        Params(command, ("$id",meeting.Id),("$folder",meeting.Folder),("$title",meeting.Title),("$created",meeting.CreatedUtc.ToString("O")),("$duration",meeting.DurationMs),("$state",meeting.State),("$error",meeting.Error));
        command.ExecuteNonQuery();
    }
    public IReadOnlyList<Meeting> ListMeetings()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Folder,Title,CreatedUtc,DurationMs,State,Error FROM Meetings ORDER BY CreatedUtc DESC";
        using var reader = command.ExecuteReader(); var result = new List<Meeting>();
        while (reader.Read()) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),DateTimeOffset.Parse(reader.GetString(3)),reader.GetDouble(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6)));
        return result;
    }
    public IReadOnlyList<TranscriptSegment> GetSegments(string meetingId)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,MeetingId,Channel,SourceFile,StartMs,EndMs,OriginalText,CorrectedText FROM Segments WHERE MeetingId=$id ORDER BY StartMs,Id";
        Params(command,("$id",meetingId)); using var reader = command.ExecuteReader(); var result = new List<TranscriptSegment>();
        while(reader.Read()) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetDouble(4),reader.GetDouble(5),reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7)));
        return result;
    }
    public bool IsChunkDone(string meetingId,string source,int index)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM Chunks WHERE MeetingId=$id AND Source=$source AND ChunkIndex=$index";
        Params(command,("$id",meetingId),("$source",source),("$index",index)); return command.ExecuteScalar()!=null;
    }
    public void CommitChunk(string meetingId,string source,int index,IReadOnlyList<TranscriptSegment> segments)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="INSERT OR IGNORE INTO Chunks VALUES($id,$source,$index)";
        Params(command,("$id",meetingId),("$source",source),("$index",index));
        if(command.ExecuteNonQuery()==0) { transaction.Commit(); return; }
        foreach(var segment in segments)
        {
            using var insert = connection.CreateCommand(); insert.Transaction=transaction;
            insert.CommandText="INSERT INTO Segments VALUES($id,$meeting,$channel,$source,$start,$end,$original,NULL)";
            Params(insert,("$id",segment.Id),("$meeting",meetingId),("$channel",segment.Channel),("$source",segment.SourceFile),("$start",segment.StartMs),("$end",segment.EndMs),("$original",segment.OriginalText));
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public void SaveCorrections(string meetingId,IReadOnlyDictionary<string,string> corrections)
    {
        using var connection = Open(); using var transaction=connection.BeginTransaction();
        foreach(var (id,text) in corrections)
        {
            using var check = connection.CreateCommand(); check.Transaction=transaction;
            check.CommandText="SELECT COALESCE(CorrectedText,OriginalText) FROM Segments WHERE Id=$id AND MeetingId=$meeting";
            Params(check,("$id",id),("$meeting",meetingId)); var previous=check.ExecuteScalar() as string;
            if(previous==null) throw new InvalidOperationException("El segmento no pertenece a esta reunión.");
            if(previous==text) continue;
            using var command=connection.CreateCommand(); command.Transaction=transaction;
            command.CommandText="INSERT INTO Revisions(MeetingId,SegmentId,Text,CreatedUtc) VALUES($meeting,$id,$text,$utc); UPDATE Segments SET CorrectedText=$text WHERE Id=$id";
            Params(command,("$meeting",meetingId),("$id",id),("$text",text),("$utc",DateTimeOffset.UtcNow.ToString("O"))); command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public void SaveReport(string meetingId,ExecutiveReport report)
    {
        using var connection=Open(); using var command=connection.CreateCommand();
        command.CommandText="INSERT INTO Reports(MeetingId,CreatedUtc,Json) VALUES($id,$utc,$json)";
        Params(command,("$id",meetingId),("$utc",report.GeneratedUtc.ToString("O")),("$json",JsonSerializer.Serialize(report,JsonDefaults.Options))); command.ExecuteNonQuery();
    }
    public ExecutiveReport? GetReport(string meetingId)
    {
        using var connection=Open(); using var command=connection.CreateCommand();
        command.CommandText="SELECT Json,CreatedUtc FROM Reports WHERE MeetingId=$id ORDER BY Id DESC LIMIT 1";
        Params(command,("$id",meetingId)); string json,utc;
        using(var reader=command.ExecuteReader()) { if(!reader.Read()) return null; json=reader.GetString(0); utc=reader.GetString(1); }
        var report=JsonSerializer.Deserialize<ExecutiveReport>(json,JsonDefaults.Options);
        using var revisions=connection.CreateCommand(); revisions.CommandText="SELECT COUNT(*) FROM Revisions WHERE MeetingId=$id AND CreatedUtc>$utc";
        Params(revisions,("$id",meetingId),("$utc",utc));
        if(Convert.ToInt64(revisions.ExecuteScalar())>0 && report!=null) report=report with {Status="Desactualizado · texto corregido; vuelve a generar el informe"};
        return report;
    }
}
