using System.Net;
using System.Text;
using System.Text.Json;
using LRMeeting.Core;
using LRMeeting.Storage;
using Microsoft.Data.Sqlite;

// Subprocess fixture exercises the actual CLI argument handling, JSON parsing and process cancellation.
if(args.Contains("-of"))
{
    var model=args[Array.IndexOf(args,"-m")+1];
    if(model.Contains("hang")){await Task.Delay(30000);return;}
    if(model.Contains("failure")){Console.Error.WriteLine("fixture failure");Environment.ExitCode=7;return;}
    var prefix=args[Array.IndexOf(args,"-of")+1];
    File.WriteAllText(prefix+".json","""{"transcription":[{"offsets":{"from":500,"to":1200},"text":"  Acordamos revisar el presupuesto.  "}]}""");
    return;
}
var temp=Path.Combine(Path.GetTempPath(),"lr-core-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
async Task Throws<T>(Func<Task> action,string message)where T:Exception
{try{await action();throw new Exception("Expected "+typeof(T).Name+": "+message);}catch(T){checks++;Console.WriteLine("PASS "+message);}}
try
{
    var store=new MeetingStore(Path.Combine(temp,"meetings.db"));
    var folder=Path.Combine(temp,"meeting");Directory.CreateDirectory(folder);
    var meeting=new Meeting("meeting-id",folder,"Revisión ejecutiva <equipo>",DateTimeOffset.UtcNow);
    store.SaveMeeting(meeting);
    var segment=new TranscriptSegment("s1",meeting.Id,"A","a.wav",0,1000,"Texto original");
    store.CommitChunk(meeting.Id,"a.wav",0,[segment]);store.CommitChunk(meeting.Id,"a.wav",0,[segment]);
    Check(store.GetSegments(meeting.Id).Count==1&&store.IsChunkDone(meeting.Id,"a.wav",0),"SQLite retries do not duplicate committed segments");
    var reopened=new MeetingStore(Path.Combine(temp,"meetings.db"));
    Check(reopened.ListMeetings().Count==1&&reopened.GetSegments(meeting.Id).Single().OriginalText=="Texto original","SQLite data survives a reopened store");
    store.SaveCorrections(meeting.Id,new Dictionary<string,string>{{"s1","Texto revisado"}});
    var corrected=store.GetSegments(meeting.Id).Single();
    Check(corrected.OriginalText=="Texto original"&&corrected.Text=="Texto revisado","Corrections preserve original transcription");
    var report=new ExecutiveReport(meeting.Title,"Resumen objetivo.",[new("Presupuesto","Se revisó una propuesta, sin acuerdo confirmado.",["s1"])],[],[new("Revisar cifras",null,null,["s1"])],[],[]){GeneratedUtc=DateTimeOffset.UtcNow.AddSeconds(-2),Coverage="Cobertura de prueba"};
    store.SaveReport(meeting.Id,report);
    Check(store.GetReport(meeting.Id)!.Status.StartsWith("Desactualizado"),"Corrected text invalidates an older report");
    var before=store.GetSegments(meeting.Id).Count;
    await Throws<SqliteException>(()=>{store.CommitChunk(meeting.Id,"broken.wav",0,[segment with{Id="s2"},segment]);return Task.CompletedTask;},"Failed insert rolls back the chunk checkpoint and partial segments");
    Check(!store.IsChunkDone(meeting.Id,"broken.wav",0)&&store.GetSegments(meeting.Id).Count==before,"Chunk transaction is atomic");

    var fakeFile=Path.Combine(temp,"audio with spaces.wav");File.WriteAllBytes(fakeFile,[]);
    var model=Path.Combine(temp,"model with spaces.bin");File.WriteAllText(model,"fixture");
    var settings=new EngineSettings{WhisperExecutable=Environment.ProcessPath!,WhisperModel=model};
    var whisper=new WhisperTranscriber();var chunk=new AudioChunk(fakeFile,0,0,2000);
    var recognized=await whisper.TranscribeAsync(chunk,settings,CancellationToken.None);
    Check(recognized.Single().StartMs==500&&recognized.Single().Text=="Acordamos revisar el presupuesto.","Whisper subprocess accepts spaced paths and parses timed JSON");
    var failure=Path.Combine(temp,"failure.bin");File.WriteAllText(failure,"fixture");
    await Throws<IOException>(()=>whisper.TranscribeAsync(chunk,settings with{WhisperModel=failure},CancellationToken.None),"Whisper exit failure is visible");
    var hang=Path.Combine(temp,"hang.bin");File.WriteAllText(hang,"fixture");
    using(var cancel=new CancellationTokenSource(500))await Throws<OperationCanceledException>(()=>whisper.TranscribeAsync(chunk,settings with{WhisperModel=hang},cancel.Token),"Cancelling Whisper terminates the subprocess");

    var retryFolder=Path.Combine(temp,"retry");Directory.CreateDirectory(retryFolder);
    var retryMeeting=new Meeting("retry",retryFolder,"Reintento",DateTimeOffset.UtcNow);store.SaveMeeting(retryMeeting);
    var retrySource=Path.Combine(retryFolder,"channel-A-000001.wav");File.WriteAllBytes(retrySource,[]);
    var transcriber=new RetryTranscriber();var pipeline=new MeetingPipeline(store,new FixturePreparer(),transcriber);
    await Throws<IOException>(()=>pipeline.TranscribeAsync(retryMeeting,[new(retrySource,"A")],settings,null,CancellationToken.None),"Interrupted transcription leaves completed chunks saved");
    Check(store.GetSegments(retryMeeting.Id).Count==1,"First transcript block persists before a later failure");
    await pipeline.TranscribeAsync(retryMeeting,[new(retrySource,"A")],settings,null,CancellationToken.None);
    Check(transcriber.Calls==3&&store.GetSegments(retryMeeting.Id).Count==2,"Resume only processes the unfinished block");
    await pipeline.TranscribeAsync(retryMeeting,[new(retrySource,"A")],settings,null,CancellationToken.None);
    Check(transcriber.Calls==3,"A finished source is not transcribed again");
    File.WriteAllText(Path.Combine(retryFolder,".recording"),"");
    Check(MeetingPipeline.DiscoverSources(retryFolder).Count==0,"Discovery excludes an open capture part");
    File.Delete(Path.Combine(retryFolder,".recording"));
    Check(MeetingPipeline.DiscoverSources(retryFolder).Count==1,"Discovery includes final part after capture stops");

    var timelineFolder=Path.Combine(temp,"timeline");Directory.CreateDirectory(timelineFolder);
    var utc=DateTimeOffset.Parse("2026-11-07T12:00:00Z");
    var lines=new[]{
        JsonSerializer.Serialize(new{type="format",channel="A",averageBytesPerSecond=32000}),
        JsonSerializer.Serialize(new{type="session",utc}),
        """{"type":"block","channel":"A","arrivalMs":1100,"bytes":3200,"blocks":[{"FileName":"channel-A-000001.wav","FileOffset":0,"Bytes":3200,"SourceOffset":0}]}""",
        """{"type":"block","channel":"A","arrivalMs":5100,"bytes":3200,"blocks":[{"FileName":"channel-A-000001.wav","FileOffset":3200,"Bytes":3200,"SourceOffset":0}]}"""};
    File.WriteAllLines(Path.Combine(timelineFolder,"capture.jsonl"),lines);
    var timeline=CaptureTimeline.Load(timelineFolder,utc);
    Check(timeline.Map(Path.Combine(timelineFolder,"channel-A-000001.wav"),150)==5050,"Timeline preserves callback gaps instead of compressing silence");

    foreach(var url in new[]{"https://127.0.0.1:11434","http://example.com","http://127.0.0.1:11434/path"})
        await Throws<ArgumentException>(()=>{using var invalid=new OllamaAnalyzer(settings with{OllamaUrl=url});return Task.CompletedTask;},"Reject nonlocal or unexpected Ollama endpoint: "+url);
    await Throws<ArgumentException>(()=>{using var invalid=new OllamaAnalyzer(settings with{OllamaModel="model:cloud"});return Task.CompletedTask;},"Reject cloud model names");
    using(var remoteClient=new HttpClient(new FixtureHandler(remote:true)))
    using(var remote=new OllamaAnalyzer(settings,remoteClient))await Throws<InvalidOperationException>(()=>remote.CheckAsync(CancellationToken.None),"Reject models whose Ollama metadata points to a remote host");

    var many=Enumerable.Range(0,18).Select(i=>new TranscriptSegment("source-"+i,meeting.Id,i%2==0?"A":"B","a.wav",i*1000,i*1000+900,"Tema "+i+" "+new string('x',1700))).ToArray();
    using var handler=new FixtureHandler();using var client=new HttpClient(handler);using var analyzer=new OllamaAnalyzer(settings,client);
    var analyzed=await analyzer.AnalyzeAsync(meeting,many,null,CancellationToken.None);
    Check(handler.ReportCalls>1&&analyzed.Topics.Any(p=>p.Evidence.Contains("source-17")),"Long report analyzes all batches and preserves the last topic");
    int calls=handler.ReportCalls;
    await analyzer.AnalyzeAsync(meeting,many,null,CancellationToken.None);
    Check(handler.ReportCalls==calls,"Saved analysis notes are reused on retry");
    await Throws<InvalidDataException>(()=>{OllamaAnalyzer.Validate(report with{Topics=[new("Inventado","Sin evidencia",["fake"])]},["s1"]);return Task.CompletedTask;},"Reject fabricated evidence references");
    using(var truncatedClient=new HttpClient(new FixtureHandler(truncated:true)))
    using(var truncated=new OllamaAnalyzer(settings,truncatedClient))
    {
        var uncached=meeting with{Folder=Path.Combine(temp,"truncated")};Directory.CreateDirectory(uncached.Folder);
        await Throws<InvalidDataException>(()=>truncated.AnalyzeAsync(uncached,many,null,CancellationToken.None),"Truncated model output is not published as a report");
    }
    var unsafeSegment=segment with{OriginalText="<script>alert('x')</script>"};
    await ReportExporter.ExportAsync(meeting,report with{Summary="<img src=x onerror=alert(1)>"},[unsafeSegment],CancellationToken.None);
    var html=File.ReadAllText(Path.Combine(folder,"report.html"));
    Check(!html.Contains("<script>")&&!html.Contains("<img src=x")&&html.Contains("&lt;script&gt;")&&html.Contains("Por confirmar"),"HTML export escapes source text and labels unconfirmed responsibilities");
    Console.WriteLine($"Completed {checks} core checks.");
}
finally{SqliteConnection.ClearAllPools();Directory.Delete(temp,true);}

sealed class FixturePreparer:IAudioPreparer
{
    public Task<IReadOnlyList<AudioChunk>> PrepareAsync(string sourceFile,string workingFolder,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<AudioChunk>>([new(sourceFile,0,0,1000),new(sourceFile,1,1000,1000)]);
}
sealed class RetryTranscriber:ITranscriber
{
    public int Calls;
    public Task<IReadOnlyList<RecognizedSegment>> TranscribeAsync(AudioChunk chunk,EngineSettings settings,CancellationToken cancellationToken)
    {Calls++;if(Calls==2)throw new IOException("fixture interruption");return Task.FromResult<IReadOnlyList<RecognizedSegment>>([new(0,900,"Texto "+chunk.Index)]);}
}
sealed class FixtureHandler(bool remote=false,bool truncated=false):HttpMessageHandler
{
    public int ReportCalls;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        object response;
        if(request.RequestUri!.AbsolutePath=="/api/show")response=remote?new{remote_host="https://remote.example"}:(object)new{details=new{family="test-local"}};
        else
        {
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string prompt=body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            string content;
            if(body.RootElement.GetProperty("format").GetProperty("properties").TryGetProperty("topics",out _))
            {
                ReportCalls++;
                using var data=JsonDocument.Parse(prompt[(prompt.IndexOf("DATOS:\n",StringComparison.Ordinal)+7)..]);
                var points=data.RootElement.EnumerateArray().Select(s=>new ReportPoint("Tema "+s.GetProperty("id").GetString(),"Punto objetivo de prueba.",[s.GetProperty("id").GetString()!])).ToArray();
                content=JsonSerializer.Serialize(new ExecutiveReport("Prueba","Resumen objetivo de prueba.",points,[],[],[],[]),JsonDefaults.Options);
            }
            else content="{\"summary\":\"Resumen ejecutivo consolidado de prueba.\"}";
            response=new{done=true,done_reason=truncated?"length":"stop",message=new{content}};
        }
        return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")};
    }
}
