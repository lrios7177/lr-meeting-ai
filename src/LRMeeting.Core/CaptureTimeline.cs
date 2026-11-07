using System.Text.Json;
namespace LRMeeting.Core;

// Callback-derived times preserve capture gaps; they are not hardware sample timestamps.
public sealed class CaptureTimeline
{
    private sealed record Anchor(double OffsetMs,double LengthMs,double MeetingStartMs);
    private sealed class LogState
    {
        public long Position;
        public string Remainder="";
        public double BaseMs;
        public Dictionary<string,int> Rates=new();
    }
    private readonly Dictionary<string,List<Anchor>> anchors=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,LogState> logs=new(StringComparer.OrdinalIgnoreCase);
    public static CaptureTimeline Load(string folder,DateTimeOffset meetingStart)
    {var result=new CaptureTimeline();result.Refresh(folder,meetingStart);return result;}
    // Retain offsets and only read newly appended log bytes during a long meeting.
    public void Refresh(string folder,DateTimeOffset meetingStart)
    {
        foreach(var log in Directory.GetFiles(folder,"capture.jsonl",SearchOption.AllDirectories))
        {
            if(!logs.TryGetValue(log,out var state))logs[log]=state=new();
            using var file=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(file.Length<=state.Position)continue;
            file.Seek(state.Position,SeekOrigin.Begin);
            using var reader=new StreamReader(file);
            var appended=state.Remainder+reader.ReadToEnd();state.Position=file.Position;
            int complete=appended.LastIndexOf('\n');
            if(complete<0){state.Remainder=appended;continue;}
            state.Remainder=appended[(complete+1)..];
            foreach(var line in appended[..complete].Split('\n',StringSplitOptions.RemoveEmptyEntries))
                ProcessLine(line,log,state,meetingStart);
        }
    }
    private void ProcessLine(string line,string log,LogState state,DateTimeOffset meetingStart)
    {
        try
        {
            using var json=JsonDocument.Parse(line);var root=json.RootElement;var type=root.GetProperty("type").GetString();
            if(type=="session")state.BaseMs=(root.GetProperty("utc").GetDateTimeOffset()-meetingStart).TotalMilliseconds;
            else if(type=="format"&&root.TryGetProperty("averageBytesPerSecond",out var rate))state.Rates[root.GetProperty("channel").GetString()!]=rate.GetInt32();
            else if(type=="block"&&root.TryGetProperty("blocks",out var blocks)&&state.Rates.TryGetValue(root.GetProperty("channel").GetString()!,out var bytesPerSecond)&&bytesPerSecond>0)
            {
                var arrival=root.GetProperty("arrivalMs").GetDouble();var duration=root.GetProperty("bytes").GetInt32()*1000d/bytesPerSecond;
                foreach(var block in blocks.EnumerateArray())
                {
                    var key=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(log)!,block.GetProperty("FileName").GetString()!));
                    if(!anchors.TryGetValue(key,out var list))anchors[key]=list=[];
                    list.Add(new(block.GetProperty("FileOffset").GetInt64()*1000d/bytesPerSecond,block.GetProperty("Bytes").GetInt32()*1000d/bytesPerSecond,
                        Math.Max(0,state.BaseMs+arrival-duration+block.GetProperty("SourceOffset").GetInt32()*1000d/bytesPerSecond)));
                }
            }
        }
        catch(JsonException ex){throw new InvalidDataException("Registro temporal de captura inválido: "+log,ex);}
    }
    public double Map(string file,double sourceMs)
    {
        if(!anchors.TryGetValue(Path.GetFullPath(file),out var list)||list.Count==0)return sourceMs;
        int low=0,high=list.Count-1;
        while(low<=high){int middle=low+(high-low)/2;if(list[middle].OffsetMs<=sourceMs)low=middle+1;else high=middle-1;}
        var anchor=list[Math.Max(0,high)];return Math.Max(0,anchor.MeetingStartMs+Math.Clamp(sourceMs-anchor.OffsetMs,0,anchor.LengthMs));
    }
    public bool HasMapping(string file)=>anchors.ContainsKey(Path.GetFullPath(file));
}
