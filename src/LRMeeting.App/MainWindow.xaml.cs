using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LRMeeting.Audio;
using LRMeeting.Storage;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace LRMeeting.App;
public partial class MainWindow : Window
{
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LR Meeting AI", "Recordings");
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch elapsed = new();
    private CaptureSession? session;
    private string? lastFolder, outputId, inputId;
    private bool testMode, busy, closing;
    private int poll;
    private float peakA, peakB;
    private WaveOutEvent? player;
    private AudioFileReader? reader;
    private int playbackVersion;
    public MainWindow()
    {
        InitializeComponent();
        RefreshDevices();
        timer.Tick += Tick;
        timer.Start();
        Closing += OnClosing;
    }
    private void Note(string text)
    {
        Messages.AppendText($"{DateTime.Now:T} {text}{Environment.NewLine}");
        Messages.ScrollToEnd();
    }
    private void RefreshDevices()
    {
        try
        {
            var oldOutput = (Outputs.SelectedItem as AudioDevice)?.Id;
            var oldInput = (Inputs.SelectedItem as AudioDevice)?.Id;
            var outputs = Devices.List(DataFlow.Render);
            var inputs = Devices.List(DataFlow.Capture);
            Outputs.ItemsSource = outputs; Inputs.ItemsSource = inputs;
            Outputs.SelectedItem = outputs.Find(d => d.Id == oldOutput) ?? outputs.FirstOrDefault();
            Inputs.SelectedItem = inputs.Find(d => d.Id == oldInput) ?? inputs.FirstOrDefault();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Error(Exception ex)
    {
        Status.Text = "Error: " + ex.Message;
        Note(ex.ToString());
        try
        {
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root, "diagnostics.log"), $"{DateTimeOffset.UtcNow:O} {ex}{Environment.NewLine}");
        }
        catch (Exception logError) { Note("No se pudo guardar diagnóstico: " + logError.Message); }
    }
    private async Task StartAsync(bool test)
    {
        if (busy || session != null) return;
        if (Outputs.SelectedItem is not AudioDevice output || Inputs.SelectedItem is not AudioDevice input)
        { Status.Text = "Selecciona una salida y un micrófono disponibles."; return; }
        busy = true; SetControls(false); StopPlayback();
        try
        {
            peakA = peakB = 0;
            PlayA.IsEnabled = PlayB.IsEnabled = Archive.IsEnabled = false;
            var folder = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            var next = new CaptureSession(folder);
            next.Level += (channel, peak) => { if (channel == "A") Volatile.Write(ref peakA, peak); else Volatile.Write(ref peakB, peak); };
            next.Fault += message => Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (session != next) return;
                Note("ERROR DE CAPTURA: " + message);
                await EndAsync();
                Status.Text = "Captura interrumpida: " + message;
            }));
            session = next;
            await next.StartAsync(output.Id, input.Id);
            outputId = output.Id; inputId = input.Id;
            testMode = test; elapsed.Restart();
            lastFolder = folder;
            Stop.IsEnabled = true;
            Note("Grabando dos fuentes independientes en " + folder);
        }
        catch (Exception ex) { session = null; Error(ex); SetControls(true); }
        finally { busy = false; }
    }
    private void SetControls(bool idle)
    {
        Outputs.IsEnabled = Inputs.IsEnabled = Refresh.IsEnabled = Test.IsEnabled = Record.IsEnabled = idle;
        Stop.IsEnabled = !idle;
    }
    private async Task EndAsync()
    {
        if (busy || session == null) return;
        busy = true; Stop.IsEnabled = false;
        var current = session;
        try
        {
            var warnings = await current.StopAsync();
            foreach (var warning in warnings) Note(warning);
            Status.Text = warnings.Count == 0 ? "Grabación guardada. Escucha y verifica ambos canales." : "Grabación guardada con advertencias; revisa el registro y escucha ambos canales.";
            PlayA.IsEnabled = AudioParts(current.Folder, "A").Length != 0;
            PlayB.IsEnabled = AudioParts(current.Folder, "B").Length != 0;
            Archive.IsEnabled = PlayA.IsEnabled || PlayB.IsEnabled;
        }
        catch (Exception ex) { Error(ex); }
        finally
        {
            session = null; elapsed.Stop(); busy = false;
            LevelA.Value = LevelB.Value = 0; SetControls(true);
        }
    }
    private async void Tick(object? sender, EventArgs args)
    {
        if (session == null || busy) return;
        LevelA.Value = Volatile.Read(ref peakA); LevelB.Value = Volatile.Read(ref peakB);
        // Decay even when a silent WASAPI endpoint emits no packets.
        Volatile.Write(ref peakA, peakA * 0.6f); Volatile.Write(ref peakB, peakB * 0.6f);
        Status.Text = testMode ? $"Prueba: {Math.Max(0, 30 - elapsed.Elapsed.TotalSeconds):0} s restantes" : $"Grabando · {elapsed.Elapsed:hh\\:mm\\:ss}";
        if (testMode && elapsed.Elapsed >= TimeSpan.FromSeconds(30)) { await EndAsync(); return; }
        if (++poll % 8 != 0) return;
        try
        {
            if (!Devices.List(DataFlow.Render).Any(d => d.Id == outputId) || !Devices.List(DataFlow.Capture).Any(d => d.Id == inputId))
            {
                Note("Un dispositivo seleccionado se desconectó o cambió de estado. No se cambiará de fuente sin tu elección.");
                await EndAsync();
                Status.Text = "Dispositivo desconectado. Actualiza la lista y selecciona las fuentes.";
            }
        }
        catch (Exception ex) { Error(ex); await EndAsync(); }
    }
    private static string[] AudioParts(string folder, string channel) => Directory.GetFiles(folder, $"channel-{channel}*.wav").Order(StringComparer.Ordinal).ToArray();
    private void Play(string channel)
    {
        if (session != null || lastFolder == null || busy) return;
        StopPlayback();
        var parts = AudioParts(lastFolder, channel);
        Note($"Reproduciendo canal {channel}: {parts.Length} parte(s), por la salida predeterminada de Windows.");
        PlayPart(parts, 0, playbackVersion);
    }
    private void PlayPart(string[] parts, int index, int version)
    {
        if (version != playbackVersion || index >= parts.Length) return;
        try
        {
            reader = new AudioFileReader(parts[index]);
            var output = new WaveOutEvent();
            player = output;
            output.Init(reader);
            output.PlaybackStopped += (_, args) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (version != playbackVersion || player != output) return;
                player = null; output.Dispose(); reader?.Dispose(); reader = null;
                if (args.Exception != null) { Error(args.Exception); return; }
                PlayPart(parts, index + 1, version);
            }));
            output.Play();
        }
        catch (Exception ex) { StopPlayback(); Error(ex); }
    }
    private void StopPlayback()
    {
        playbackVersion++;
        player?.Stop(); player?.Dispose(); player = null;
        reader?.Dispose(); reader = null;
    }
    private async void ArchiveClick(object sender, RoutedEventArgs e)
    {
        if (busy || session != null || lastFolder == null) return;
        busy = true; Archive.IsEnabled = false; StopPlayback();
        SetControls(false); Stop.IsEnabled = false;
        try
        {
            var progress = new Progress<double>(value => Status.Text = $"Guardando archivo de reunión: {value:P0}. Se conservan los WAV originales.");
            var path = await MeetingArchive.CreateAsync(lastFolder, progress);
            Status.Text = "Archivo único guardado. Incluye ambos canales y sus marcas de tiempo.";
            Note("Reunión guardada: " + path);
        }
        catch (Exception ex) { Error(ex); }
        finally { busy = false; SetControls(true); Archive.IsEnabled = true; }
    }
    private async void TestClick(object sender, RoutedEventArgs e) => await StartAsync(true);
    private async void RecordClick(object sender, RoutedEventArgs e) => await StartAsync(false);
    private async void StopClick(object sender, RoutedEventArgs e) => await EndAsync();
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshDevices();
    private void PlayAClick(object sender, RoutedEventArgs e) => Play("A");
    private void PlayBClick(object sender, RoutedEventArgs e) => Play("B");
    private void StopPlaybackClick(object sender, RoutedEventArgs e) => StopPlayback();
    private void OpenFolderClick(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(root); Process.Start(new ProcessStartInfo(lastFolder ?? root) { UseShellExecute = true }); }
        catch (Exception ex) { Error(ex); }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (busy) { Status.Text = "Espera a que termine la operación antes de cerrar."; return; }
        await EndAsync(); StopPlayback(); timer.Stop(); closing = true; Close();
    }
}
