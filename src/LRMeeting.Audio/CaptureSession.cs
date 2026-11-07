using System.Diagnostics;
using System.Collections.Concurrent;
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
    private readonly ConcurrentQueue<string> errors = new();
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
            Report("inicio", ex);
            await StopAsync();
            throw;
        }
    }
    private void Add(string name, MMDevice device, bool loopback)
    {
        WasapiCapture? capture = null;
        RotatingWaveWriter? writer = null;
        try
        {
            capture = loopback ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
            writer = new RotatingWaveWriter(folder, name, capture.WaveFormat, (long)capture.WaveFormat.AverageBytesPerSecond * 30);
            var c = new Channel(name, device, capture, writer);
            capture.DataAvailable += (_, e) => Receive(c, e);
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) Report(name, e.Exception);
                else if (Volatile.Read(ref stopping) == 0) Report(name, new IOException("Captura detenida inesperadamente."));
                c.Stopped.TrySetResult();
            };
            Write(new { type = "format", channel = name, format = capture.WaveFormat.ToString(), averageBytesPerSecond = capture.WaveFormat.AverageBytesPerSecond });
            channels.Add(c);
        }
        catch
        {
            Cleanup(name, () => writer?.Dispose());
            Cleanup(name, () => capture?.Dispose());
            Cleanup(name, device.Dispose);
            throw;
        }
    }
    private void Receive(Channel c, WaveInEventArgs e)
    {
        try
        {
            lock (c.Gate)
            {
                if (c.Closed) return;
                var blocks = c.Writer.Write(e.Buffer, e.BytesRecorded);
                var peak = AudioLevels.Peak(e.Buffer, e.BytesRecorded, c.Capture.WaveFormat);
                if (peak > 0.001f) c.HeardAudio = true;
                Write(new { type = "block", channel = c.Name, arrivalMs = clock.Elapsed.TotalMilliseconds,
                    blocks, bytes = e.BytesRecorded, peak });
                Level?.Invoke(c.Name, peak);
            }
        }
        catch (Exception ex) { Report(c.Name, ex); }
    }
    private void Report(string name, Exception ex)
    {
        var message = $"Canal {name}: {ex.Message}";
        try { Write(new { type = "error", channel = name, elapsedMs = clock.Elapsed.TotalMilliseconds, error = ex.ToString() }); }
        catch (Exception logError) { message += $" No se pudo escribir el registro: {logError.Message}"; }
        errors.Enqueue(message);
        Fault?.Invoke(message);
    }
    private void Cleanup(string name, Action action)
    {
        try { action(); }
        catch (Exception ex) { Report(name, ex); }
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
                Cleanup(c.Name, c.Writer.Dispose);
            }
            Cleanup(c.Name, c.Capture.Dispose);
            Cleanup(c.Name, c.Device.Dispose);
            if (!c.HeardAudio) warnings.Add($"Canal {c.Name}: no se detectó audio audible. Puede ser silencio normal; repita hablando o reproduciendo sonido.");
            Cleanup(c.Name, () => Write(new { type = "channel-end", channel = c.Name, c.StartMilliseconds, c.HeardAudio, bytes = c.Writer.Length, parts = c.Writer.PartCount }));
        }
        clock.Stop();
        lock (logLock)
        {
            var closingLog = log;
            log = null;
            Cleanup("registro", () => closingLog?.Dispose());
        }
        warnings.AddRange(errors.Distinct());
        return warnings;
    }
    private sealed class Channel(string name, MMDevice device, WasapiCapture capture, RotatingWaveWriter writer)
    {
        public string Name = name;
        public MMDevice Device = device;
        public WasapiCapture Capture = capture;
        public RotatingWaveWriter Writer = writer;
        public object Gate = new();
        public TaskCompletionSource Stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HeardAudio, Started, Closed;
        public double StartMilliseconds;
    }
}
