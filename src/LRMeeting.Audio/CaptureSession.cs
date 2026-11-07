using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace LRMeeting.Audio;

// Callback arrival times share a monotonic origin. They are not hardware sample timestamps.
public sealed class CaptureSession
{
    private readonly Stopwatch clock = new();
    private readonly List<Channel> channels = [];
    private readonly object logLock = new();
    private readonly string folder;
    private StreamWriter? log;
    private int stopping;
    public event Action<string, float>? Level;
    public event Action<string>? Fault;
    public string Folder => folder;
    public CaptureSession(string folder) => this.folder = folder;
    public async Task StartAsync(string outputId, string inputId)
    {
        Directory.CreateDirectory(folder);
        log = new StreamWriter(Path.Combine(folder, "capture.jsonl"), false) { AutoFlush = true };
        clock.Start();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            Add("A", enumerator.GetDevice(outputId), true);
            Add("B", enumerator.GetDevice(inputId), false);
            Write(new { type = "session", utc = DateTimeOffset.UtcNow, outputId, inputId,
                timestampMeaning = "monotonic callback arrival; approximate alignment, not sample accurate" });
            foreach (var channel in channels)
            {
                channel.StartMilliseconds = clock.Elapsed.TotalMilliseconds;
                channel.Capture.StartRecording();
                channel.Started = true;
            }
        }
        catch (Exception ex)
        {
            Write(new { type = "start-error", error = ex.ToString() });
            await StopAsync();
            throw;
        }
    }
    private void Add(string name, MMDevice device, bool loopback)
    {
        WasapiCapture? capture = null;
        WaveFileWriter? writer = null;
        try
        {
            capture = loopback ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
            writer = new WaveFileWriter(Path.Combine(folder, $"channel-{name}.wav"), capture.WaveFormat);
            var c = new Channel(name, device, capture, writer);
            channels.Add(c);
            capture.DataAvailable += (_, e) => Receive(c, e);
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) Report(name, e.Exception);
                else if (Volatile.Read(ref stopping) == 0) Report(name, new IOException("Captura detenida inesperadamente."));
                c.Stopped.TrySetResult();
            };
            Write(new { type = "format", channel = name, format = capture.WaveFormat.ToString() });
        }
        catch { writer?.Dispose(); capture?.Dispose(); device.Dispose(); throw; }
    }
    private void Receive(Channel c, WaveInEventArgs e)
    {
        try
        {
            lock (c.Gate)
            {
                if (c.Closed) return;
                // Classic WAV has a 4 GiB limit: fail explicitly before corrupting it.
                if (c.Writer.Length + e.BytesRecorded > uint.MaxValue - 1024L)
                    throw new IOException("Límite WAV alcanzado; detenga la sesión y comience otra.");
                var offset = c.Writer.Length;
                c.Writer.Write(e.Buffer, 0, e.BytesRecorded);
                var peak = AudioLevels.Peak(e.Buffer, e.BytesRecorded, c.Capture.WaveFormat);
                if (peak > 0.001f) c.HeardAudio = true;
                Write(new { type = "block", channel = c.Name, arrivalMs = clock.Elapsed.TotalMilliseconds,
                    fileOffset = offset, bytes = e.BytesRecorded, peak });
                Level?.Invoke(c.Name, peak);
            }
        }
        catch (Exception ex) { Report(c.Name, ex); }
    }
    private void Report(string name, Exception ex)
    {
        Write(new { type = "error", channel = name, elapsedMs = clock.Elapsed.TotalMilliseconds, error = ex.ToString() });
        Fault?.Invoke($"Canal {name}: {ex.Message}");
    }
    private void Write(object value)
    {
        lock (logLock) log?.WriteLine(JsonSerializer.Serialize(value));
    }
    public async Task<IReadOnlyList<string>> StopAsync()
    {
        if (Interlocked.Exchange(ref stopping, 1) != 0) return [];
        var warnings = new List<string>();
        foreach (var c in channels)
        {
            if (!c.Started) continue;
            try { c.Capture.StopRecording(); }
            catch (Exception ex) { Report(c.Name, ex); c.Stopped.TrySetResult(); }
        }
        foreach (var c in channels)
        {
            if (c.Started)
            {
                try { await c.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (TimeoutException ex) { Report(c.Name, ex); }
            }
            lock (c.Gate)
            {
                c.Closed = true;
                c.Writer.Dispose();
            }
            c.Capture.Dispose(); c.Device.Dispose();
            if (!c.HeardAudio) warnings.Add($"Canal {c.Name}: no se detectó audio audible. Puede ser silencio normal; repita hablando o reproduciendo sonido.");
            Write(new { type = "channel-end", channel = c.Name, c.StartMilliseconds, c.HeardAudio });
        }
        clock.Stop();
        lock (logLock) { log?.Dispose(); log = null; }
        return warnings;
    }
    private sealed class Channel(string name, MMDevice device, WasapiCapture capture, WaveFileWriter writer)
    {
        public string Name = name;
        public MMDevice Device = device;
        public WasapiCapture Capture = capture;
        public WaveFileWriter Writer = writer;
        public object Gate = new();
        public TaskCompletionSource Stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HeardAudio, Started, Closed;
        public double StartMilliseconds;
    }
}
