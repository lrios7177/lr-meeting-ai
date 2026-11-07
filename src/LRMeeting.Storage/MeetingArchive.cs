using System.IO.Compression;
using System.Text.Json;
namespace LRMeeting.Storage;

public static class MeetingArchive
{
    // ZIP64 is selected automatically. Model files, working data and the shared DB are excluded.
    public static async Task<string> CreateAsync(string folder,IProgress<double>? progress=null,CancellationToken cancellationToken=default)
    {
        folder=Path.GetFullPath(folder);
        var roots=Directory.EnumerateDirectories(folder).Where(p=>Path.GetFileName(p).StartsWith("take-",StringComparison.Ordinal)).Prepend(folder).ToArray();
        if(roots.Any(p=>File.Exists(Path.Combine(p,".recording"))))throw new InvalidOperationException("Detén la captura antes de guardar el paquete.");
        var files=roots.SelectMany(Directory.GetFiles).Where(f=>Path.GetExtension(f).Equals(".wav",StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(f)=="capture.jsonl"
            || new[]{"report.html","report.json","transcript.txt","transcript.json","meeting-info.json"}.Contains(Path.GetFileName(f))
            || Path.GetFileName(f).StartsWith("imported.",StringComparison.Ordinal)).Order().ToArray();
        if(!files.Any(f=>Path.GetExtension(f).Equals(".wav",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(f).StartsWith("imported.",StringComparison.Ordinal)))
            throw new InvalidOperationException("La reunión no contiene audio.");
        var target=folder+".lrmeeting.zip";
        if(File.Exists(target))target=folder+"-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".lrmeeting.zip";
        var partial=target+".partial-"+Guid.NewGuid().ToString("N");
        long total=files.Sum(f=>new FileInfo(f).Length),copied=0;
        var lastProgress=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072,FileOptions.Asynchronous))
            {
                using var zip=new ZipArchive(output,ZipArchiveMode.Create,leaveOpen:true);
                var manifest=zip.CreateEntry("meeting.json");
                await using(var manifestStream=manifest.Open())
                    await JsonSerializer.SerializeAsync(manifestStream,new{schemaVersion=2,createdUtc=DateTimeOffset.UtcNow,
                        sources=new[]{"A: salida de Windows","B: micrófono","Importado: archivo externo, cuando corresponda"},
                        timestampMeaning="callback arrival, approximate alignment; see capture.jsonl",
                        files=files.Select(f=>new{name=Path.GetRelativePath(folder,f).Replace('\\','/'),bytes=new FileInfo(f).Length})},cancellationToken:cancellationToken);
                var buffer=new byte[131072];
                foreach(var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry=zip.CreateEntry(Path.GetRelativePath(folder,file).Replace('\\','/'),CompressionLevel.NoCompression);
                    await using var destination=entry.Open();
                    await using var source=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,buffer.Length,FileOptions.Asynchronous|FileOptions.SequentialScan);
                    int read;
                    while((read=await source.ReadAsync(buffer,cancellationToken))!=0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0,read),cancellationToken);copied+=read;
                        if(lastProgress.ElapsedMilliseconds>=100){progress?.Report(total==0?1:(double)copied/total);lastProgress.Restart();}
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();File.Move(partial,target,overwrite:false);progress?.Report(1);return target;
        }
        finally{if(File.Exists(partial))File.Delete(partial);}
    }
}
