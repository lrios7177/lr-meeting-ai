using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using LRMeeting.Core;
namespace LRMeeting.App;

internal sealed class LocalOllamaHost(string dataRoot):IDisposable
{
    internal const string Endpoint="http://127.0.0.1:11435";
    private Process? owned;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object logGate=new();
    internal static string BundleFolder=>Path.Combine(AppContext.BaseDirectory,"AI");
    internal static bool Available=>File.Exists(Path.Combine(BundleFolder,"bundle.json"));
    internal static EngineSettings? BundledSettings()
    {
        if(!Available)return null;
        using var bundle=JsonDocument.Parse(File.ReadAllText(Path.Combine(BundleFolder,"bundle.json")));
        var executable=Directory.GetFiles(Path.Combine(BundleFolder,"whisper"),"whisper-cli.exe",SearchOption.AllDirectories).FirstOrDefault();
        var model=Path.Combine(BundleFolder,"models",bundle.RootElement.GetProperty("whisperModel").GetString()!);
        var ollamaModel=bundle.RootElement.GetProperty("ollamaModel").GetString()!;
        if(executable==null||!File.Exists(model)||!Directory.Exists(Path.Combine(BundleFolder,"models","ollama")))throw new InvalidDataException("El paquete IA está incompleto. Reinstala la edición completa.");
        return new(){WhisperExecutable=executable,WhisperModel=model,OllamaUrl=Endpoint,OllamaModel=ollamaModel};
    }
    internal async Task EnsureStartedAsync(EngineSettings settings,CancellationToken cancellationToken)
    {
        if(settings.OllamaUrl.TrimEnd('/')!=Endpoint)return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(1)}){Timeout=TimeSpan.FromSeconds(2)};
            async Task<bool> Ready()
            {
                try{using var response=await http.GetAsync(Endpoint+"/api/version",cancellationToken);return response.IsSuccessStatusCode;}
                catch(HttpRequestException){return false;}
                catch(TaskCanceledException)when(!cancellationToken.IsCancellationRequested){return false;}
            }
            if(await Ready())return;
            if(!Available)throw new FileNotFoundException("Reinstala la edición completa o configura un Ollama local en Ajustes.");
            var executable=Directory.GetFiles(Path.Combine(BundleFolder,"ollama"),"ollama.exe",SearchOption.AllDirectories).FirstOrDefault()
                ??throw new FileNotFoundException("Falta el ejecutable incluido de Ollama.");
            if(owned!=null){StopOwned();}
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(executable)!};
            start.ArgumentList.Add("serve");
            start.Environment["OLLAMA_HOST"]="127.0.0.1:11435";
            start.Environment["OLLAMA_MODELS"]=Path.Combine(BundleFolder,"models","ollama");
            start.Environment["OLLAMA_NO_CLOUD"]="1";
            start.Environment["CUDA_VISIBLE_DEVICES"]="-1";
            start.Environment["OLLAMA_VULKAN"]="0";
            owned=new Process{StartInfo=start};
            void Log(string? line)
            {
                if(line==null)return;
                lock(logGate)
                {
                    try{Directory.CreateDirectory(dataRoot);File.AppendAllText(Path.Combine(dataRoot,"ollama.log"),DateTimeOffset.UtcNow.ToString("O")+" "+line+Environment.NewLine);}
                    catch(IOException){Debug.WriteLine(line);}
                    catch(UnauthorizedAccessException){Debug.WriteLine(line);}
                }
            }
            owned.OutputDataReceived+=(_,e)=>Log(e.Data);owned.ErrorDataReceived+=(_,e)=>Log(e.Data);
            if(!owned.Start())throw new IOException("No se pudo iniciar Ollama incluido.");
            owned.BeginOutputReadLine();owned.BeginErrorReadLine();
            for(int i=0;i<60;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(owned.HasExited)throw new IOException("Ollama se detuvo. Consulta ollama.log en la carpeta de datos.");
                if(await Ready())return;
                await Task.Delay(500,cancellationToken);
            }
            throw new TimeoutException("Ollama incluido no respondió al iniciar. Consulta ollama.log.");
        }
        catch{StopOwned();throw;}
        finally{gate.Release();}
    }
    private void StopOwned()
    {
        if(owned==null)return;
        try{if(!owned.HasExited){owned.Kill(entireProcessTree:true);owned.WaitForExit(5000);}}
        catch(InvalidOperationException){ }
        finally{owned.Dispose();owned=null;}
    }
    public void Dispose(){StopOwned();gate.Dispose();}
}
