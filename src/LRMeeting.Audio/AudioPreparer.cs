using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LRMeeting.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace LRMeeting.Audio;

public sealed class AudioPreparer : IAudioPreparer
{
    public Task<IReadOnlyList<AudioChunk>> PrepareAsync(string sourceFile,string workingFolder,CancellationToken cancellationToken) => Task.Run<IReadOnlyList<AudioChunk>>(()=>
    {
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(sourceFile))))[..20];
        var folder=Path.Combine(workingFolder,key); Directory.CreateDirectory(folder);
        var manifest=Path.Combine(folder,"chunks.json");
        var info=new FileInfo(sourceFile);
        if(File.Exists(manifest))
        {
            var cached=JsonSerializer.Deserialize<PreparedManifest>(File.ReadAllText(manifest),JsonDefaults.Options);
            if(cached!=null && cached.SourceBytes==info.Length && cached.SourceModifiedUtc==info.LastWriteTimeUtc && cached.Chunks.All(c=>File.Exists(c.FilePath))) return cached.Chunks;
        }
        using var reader=new AudioFileReader(sourceFile);
        if(reader.Length==0&&info.Length>4096)throw new InvalidDataException("El audio parece no estar finalizado o usa un formato no decodificable. Revisa el último WAV de la sesión interrumpida.");
        ISampleProvider source=reader;
        if(source.WaveFormat.Channels>1) source=new Downmix(source);
        if(source.WaveFormat.SampleRate!=16000) source=new WdlResamplingSampleProvider(source,16000);
        var result=new List<AudioChunk>();
        var samples=new float[16000*30];
        long consumed=0;
        while(true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count=0;
            while(count<samples.Length)
            {
                int read=source.Read(samples,count,samples.Length-count);
                if(read==0) break;
                count+=read;
                cancellationToken.ThrowIfCancellationRequested();
            }
            if(count==0) break;
            var path=Path.Combine(folder,$"{result.Count:D6}.wav");
            float peak=0; for(int i=0;i<count;i++) peak=Math.Max(peak,Math.Abs(samples[i]));
            using(var writer=new WaveFileWriter(path,new WaveFormat(16000,16,1))) writer.WriteSamples(samples,0,count);
            result.Add(new(path,result.Count,consumed*1000d/16000,count*1000d/16000,peak>0.001f));
            consumed+=count;
        }
        File.WriteAllText(manifest,JsonSerializer.Serialize(new PreparedManifest(info.Length,info.LastWriteTimeUtc,result),JsonDefaults.Options));
        return result;
    },cancellationToken);
    private sealed record PreparedManifest(long SourceBytes,DateTime SourceModifiedUtc,List<AudioChunk> Chunks);
    private sealed class Downmix(ISampleProvider input):ISampleProvider
    {
        public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate,1);
        private float[] buffer=[];
        public int Read(float[] destination,int offset,int count)
        {
            int channels=input.WaveFormat.Channels;
            if(buffer.Length<count*channels) buffer=new float[count*channels];
            int read=input.Read(buffer,0,count*channels), frames=read/channels;
            for(int i=0;i<frames;i++) { float sum=0; for(int c=0;c<channels;c++) sum+=buffer[i*channels+c]; destination[offset+i]=sum/channels; }
            return frames;
        }
    }
}
