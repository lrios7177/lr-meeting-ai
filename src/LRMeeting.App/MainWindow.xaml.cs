using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LRMeeting.Audio;
using LRMeeting.Core;
using LRMeeting.Storage;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace LRMeeting.App;

public partial class MainWindow : Window
{
    private readonly string dataRoot;
    private readonly string root;
    private readonly MeetingStore store;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly ObservableCollection<SegmentRow> rows = [];
    private readonly Stopwatch elapsed = new();
    private CaptureSession? session;
    private Meeting? selected;
    private EngineSettings settings = new();
    private CancellationTokenSource? processing;
    private Task? liveTask;
    private volatile bool meetingActive;
    private string? outputId,inputId;
    private bool testMode,busy,closing,loadingHistory;
    private int poll,take;
    private float peakA,peakB;
    private WaveOutEvent? player;
    private AudioFileReader? reader;
    private int playbackVersion;
    private readonly bool smoke = Environment.GetCommandLineArgs().Contains("--smoke-test");
    public MainWindow()
    {
        InitializeComponent();
        var args=Environment.GetCommandLineArgs();
        dataRoot=smoke ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[Array.IndexOf(args,"--smoke-test")+1]))!,"smoke-data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LR Meeting AI");
        root=Path.Combine(dataRoot,"Recordings"); Directory.CreateDirectory(root);
        store=new MeetingStore(Path.Combine(dataRoot,"meetings.db"));
        TranscriptGrid.ItemsSource=rows;
        try
        {
            var path=Path.Combine(dataRoot,"settings.json");
            if(File.Exists(path)) settings=JsonSerializer.Deserialize<EngineSettings>(File.ReadAllText(path),JsonDefaults.Options) ?? new();
        }
        catch(Exception ex) { Error(ex); }
        WhisperPath.Text=settings.WhisperExecutable; ModelPath.Text=settings.WhisperModel;
        LanguageChoice.SelectedIndex=settings.Language=="en" ? 1:0; OllamaUrl.Text=settings.OllamaUrl;
        OllamaModel.Text=settings.OllamaModel; AutoProcess.IsChecked=settings.AutoProcess;
        RecoverInterrupted(); LoadHistory();
        if(smoke)
        {
            Outputs.ItemsSource=new[]{new AudioDevice("demo-output","Salida de Windows · vista de prueba")}; Outputs.SelectedIndex=0;
            Inputs.ItemsSource=new[]{new AudioDevice("demo-input","Micrófono · vista de prueba")}; Inputs.SelectedIndex=0;
            GlobalStatus.Text="Vista de prueba de interfaz · sin grabación ni modelos ejecutados";
        }
        else RefreshDevices();
        timer.Tick+=Tick;timer.Start();Closing+=OnClosing;
        UpdateControls();
    }
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if(!smoke) return;
        try
        {
            await Task.Delay(700);
            await VerifyAudioPreparationAsync();
            var args=Environment.GetCommandLineArgs(); var path=Path.GetFullPath(args[Array.IndexOf(args,"--smoke-test")+1]);
            var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(this);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var output=File.Create(path))encoder.Save(output);
            NavigateTo(4);await Task.Delay(300);
            var settingsBitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);settingsBitmap.Render(this);
            var settingsEncoder=new PngBitmapEncoder();settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap));
            using(var output=File.Create(Path.ChangeExtension(path,"settings.png")))settingsEncoder.Save(output);
            File.WriteAllText(Path.ChangeExtension(path,"checks.txt"),"WPF opened and rendered; SQLite native runtime loaded; audio conversion and silence detection passed.");
            closing=true;Close();
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(dataRoot,"smoke-error.log"),ex.ToString());Application.Current.Shutdown(1);}
    }
    private void RecoverInterrupted()
    {
        foreach(var meeting in store.ListMeetings())
        {
            if(meeting.State is "Grabando" or "Pausada" or "Transcribiendo" or "Analizando")
                store.SaveMeeting(meeting with{State="Interrumpida · reanudar procesamiento",Error="La sesión anterior no finalizó normalmente. Revisa los últimos WAV antes de procesar."});
            if(Directory.Exists(meeting.Folder))
                foreach(var marker in Directory.GetFiles(meeting.Folder,".recording",SearchOption.AllDirectories)) File.Delete(marker);
        }
    }
    private void NavigateTo(int index)
    {
        Pages.SelectedIndex=index;
        new[]{Nav0,Nav1,Nav2,Nav3,Nav4}[index].IsChecked=true;
        PageTitle.Text=new[]{"Tu próxima reunión, más clara.","Una conversación que puedes revisar.","Decisiones listas para avanzar.","La memoria de tus reuniones.","Configura tu espacio local."}[index];
    }
    private void NavigateClick(object sender,RoutedEventArgs e)
    {
        if(sender is RadioButton button&&int.TryParse(button.Tag?.ToString(),out var index))NavigateTo(index);
    }
    private async Task VerifyAudioPreparationAsync()
    {
        var source=Path.Combine(dataRoot,"conversion-check.wav");
        await Task.Run(()=>
        {
            using var writer=new WaveFileWriter(source,WaveFormat.CreateIeeeFloatWaveFormat(48000,2));
            var samples=new float[4800*2];
            for(int i=0;i<4800;i++)samples[2*i]=samples[2*i+1]=(float)Math.Sin(2*Math.PI*440*i/48000)*0.1f;
            for(int i=0;i<610;i++)writer.WriteSamples(samples,0,samples.Length);
        });
        var chunks=await new AudioPreparer().PrepareAsync(source,Path.Combine(dataRoot,"conversion"),CancellationToken.None);
        if(chunks.Count!=3||Math.Abs(chunks.Sum(c=>c.DurationMs)-61000)>10||chunks.Any(c=>!c.HasAudio))throw new InvalidDataException("Audio conversion smoke check failed.");
        foreach(var chunk in chunks)
        {
            using var reader=new WaveFileReader(chunk.FilePath);
            if(reader.WaveFormat.SampleRate!=16000||reader.WaveFormat.Channels!=1||reader.WaveFormat.BitsPerSample!=16)throw new InvalidDataException("Whisper input format check failed.");
        }
        var silence=Path.Combine(dataRoot,"silence-check.wav");
        using(var writer=new WaveFileWriter(silence,new WaveFormat(16000,16,1)))writer.Write(new byte[32000],0,32000);
        var silentChunks=await new AudioPreparer().PrepareAsync(silence,Path.Combine(dataRoot,"conversion"),CancellationToken.None);
        if(silentChunks.Single().HasAudio)throw new InvalidDataException("Silence detection check failed.");
    }
    private void Note(string text)
    {
        Messages.AppendText($"{DateTime.Now:T} {text}{Environment.NewLine}");
        if(Messages.Text.Length>50000)Messages.Text=Messages.Text[^35000..];
        Messages.ScrollToEnd();
    }
    private void SetStatus(string text){Status.Text=text;GlobalStatus.Text=text;}
    private void Error(Exception ex)
    {
        SetStatus("Error: "+ex.Message);Note(ex.ToString());
        try{File.AppendAllText(Path.Combine(root,"diagnostics.log"),$"{DateTimeOffset.UtcNow:O} {ex}{Environment.NewLine}");}
        catch(Exception logError){Note("No se pudo guardar diagnóstico: "+logError.Message);}
    }
    private async Task Guard(Func<Task> action){try{await action();}catch(Exception ex){Error(ex);UpdateControls();}}
    private void RefreshDevices()
    {
        try
        {
            var oldOutput=(Outputs.SelectedItem as AudioDevice)?.Id;var oldInput=(Inputs.SelectedItem as AudioDevice)?.Id;
            var outputs=Devices.List(DataFlow.Render);var inputs=Devices.List(DataFlow.Capture);
            Outputs.ItemsSource=outputs;Inputs.ItemsSource=inputs;
            Outputs.SelectedItem=outputs.Find(d=>d.Id==oldOutput)??outputs.FirstOrDefault();
            Inputs.SelectedItem=inputs.Find(d=>d.Id==oldInput)??inputs.FirstOrDefault();
        }catch(Exception ex){Error(ex);}
    }
    private EngineSettings ReadSettings()=>new()
    {
        WhisperExecutable=WhisperPath.Text.Trim(),WhisperModel=ModelPath.Text.Trim(),
        Language=(LanguageChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"es",
        OllamaUrl=OllamaUrl.Text.Trim(),OllamaModel=OllamaModel.Text.Trim(),AutoProcess=AutoProcess.IsChecked==true
    };
    private async Task SaveSettingsAsync()
    {
        settings=ReadSettings();
        await ReportExporter.AtomicWriteAsync(Path.Combine(dataRoot,"settings.json"),JsonSerializer.Serialize(settings,JsonDefaults.Options),CancellationToken.None);
    }
    private void UpdateControls()
    {
        bool idle=!busy&&!meetingActive;
        Outputs.IsEnabled=Inputs.IsEnabled=Refresh.IsEnabled=Test.IsEnabled=Record.IsEnabled=MeetingTitle.IsEnabled=AutoProcess.IsEnabled=idle;
        Stop.IsEnabled=meetingActive&&!busy;Pause.IsEnabled=meetingActive&&!busy&&!testMode;
        Pause.Content=session==null?"Reanudar":"Pausar";
        HistoryGrid.IsEnabled=idle;TranscriptGrid.IsReadOnly=!idle;SaveText.IsEnabled=idle&&selected!=null;
        Process.IsEnabled=Regenerate.IsEnabled=idle&&selected!=null;Import.IsEnabled=idle;
        PlayA.IsEnabled=idle&&selected!=null&&HasAudio(selected.Folder,"A");
        PlayB.IsEnabled=idle&&selected!=null&&HasAudio(selected.Folder,"B");
        Archive.IsEnabled=idle&&selected!=null;
        CancelAI.IsEnabled=processing!=null&&!processing.IsCancellationRequested;
        CheckEngines.IsEnabled=idle;
    }
    private static bool HasAudio(string folder,string channel)=>Directory.Exists(folder)&&Directory.GetFiles(folder,$"channel-{channel}*.wav",SearchOption.AllDirectories).Length!=0;
    private async Task StartAsync(bool test)
    {
        if(busy||meetingActive)return;
        if(Outputs.SelectedItem is not AudioDevice output||Inputs.SelectedItem is not AudioDevice input){SetStatus("Selecciona una salida y un micrófono disponibles.");return;}
        busy=true;UpdateControls();StopPlayback();
        try
        {
            await SaveSettingsAsync();
            var folder=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(folder);
            selected=new(Guid.NewGuid().ToString("N"),folder,test?"Prueba de audio":string.IsNullOrWhiteSpace(MeetingTitle.Text)?"Nueva reunión":MeetingTitle.Text.Trim(),DateTimeOffset.UtcNow,State:"Grabando");
            store.SaveMeeting(selected);rows.Clear();ShowReport(null);take=0;testMode=test;
            outputId=output.Id;inputId=input.Id;
            await BeginTakeAsync();
            meetingActive=true;elapsed.Restart();RecordState("Grabando dos fuentes");
            Note("Grabando en "+folder);SetStatus(test?"Prueba en curso. Reproduce sonido y habla al micrófono.":"Reunión en curso. Los WAV se guardan independientemente de la IA.");
            if(!test&&settings.AutoProcess)
            {
                try
                {
                    settings.ValidateWhisper();processing=new();
                    var meeting=selected;var config=settings;var token=processing.Token;
                    liveTask=Task.Run(async()=>
                    {
                        try{await LiveTranscriptionAsync(meeting,config,token);}
                        catch(OperationCanceledException){throw;}
                        catch(Exception ex)
                        {
                            await Dispatcher.InvokeAsync(()=>{Note("Transcripción interrumpida: "+ex.Message);SetStatus("El audio continúa grabándose; la transcripción requiere revisión: "+ex.Message);});
                            throw;
                        }
                    });
                }
                catch(Exception ex){Note("IA pendiente de configuración: "+ex.Message);SetStatus("Grabando audio. Configura Whisper en Ajustes para procesar después.");}
            }
        }
        catch(Exception ex)
        {
            if(selected!=null)store.SaveMeeting(selected with{State="Error de captura",Error=ex.Message});
            session=null;meetingActive=false;Error(ex);
        }
        finally{busy=false;LoadHistory();UpdateControls();}
    }
    private async Task BeginTakeAsync()
    {
        var folder=Path.Combine(selected!.Folder,$"take-{++take:D4}");Directory.CreateDirectory(folder);
        var marker=Path.Combine(folder,".recording");File.WriteAllText(marker,"");
        var next=new CaptureSession(folder);
        next.Level+=(channel,peak)=>{if(channel=="A")Volatile.Write(ref peakA,peak);else Volatile.Write(ref peakB,peak);};
        next.Fault+=message=>Dispatcher.BeginInvoke(new Action(async()=>
        {
            if(session!=next)return;
            Note("ERROR DE CAPTURA: "+message);
            if(busy)return;
            await Guard(()=>EndAsync(captureFailed:true));SetStatus("Captura interrumpida: "+message+". El audio previo está conservado.");
        }));
        session=next;
        try{await next.StartAsync(outputId!,inputId!);}
        catch{session=null;if(File.Exists(marker))File.Delete(marker);throw;}
    }
    private MeetingPipeline NewPipeline()
    {
        var pipeline=new MeetingPipeline(store,new AudioPreparer(),new WhisperTranscriber());
        pipeline.SegmentAdded+=segment=>Dispatcher.BeginInvoke(new Action(()=>
        {
            if(selected?.Id==segment.MeetingId){rows.Add(new(segment));TranscriptState.Text=$"{rows.Count} segmentos guardados · revisión disponible al detener";}
        }));
        return pipeline;
    }
    private IProgress<string> ProcessingProgress()=>new Progress<string>(text=>{SetStatus(text);TranscriptState.Text=text;});
    private async Task LiveTranscriptionAsync(Meeting meeting,EngineSettings config,CancellationToken token)
    {
        var pipeline=NewPipeline();
        // Progress<T> must be created on the dispatcher to marshal updates back to WPF.
        var progress=await Dispatcher.InvokeAsync(ProcessingProgress);
        while(meetingActive)
        {
            await pipeline.TranscribeAsync(meeting,MeetingPipeline.DiscoverSources(meeting.Folder),config,progress,token).ConfigureAwait(false);
            await Task.Delay(2000,token).ConfigureAwait(false);
        }
        await pipeline.TranscribeAsync(meeting,MeetingPipeline.DiscoverSources(meeting.Folder),config,progress,token).ConfigureAwait(false);
    }
    private void RecordState(string state){RecordingState.Text=state;}
    private async Task StopTakeAsync()
    {
        if(session==null)return;
        var current=session;
        try{foreach(var warning in await current.StopAsync())Note(warning);}
        finally
        {
            session=null;LevelA.Value=LevelB.Value=0;
            var marker=Path.Combine(current.Folder,".recording");if(File.Exists(marker))File.Delete(marker);
        }
    }
    private async Task EndAsync(bool captureFailed=false)
    {
        if(busy||!meetingActive)return;
        busy=true;UpdateControls();
        try
        {
            if(captureFailed)processing?.Cancel();
            await StopTakeAsync();meetingActive=false;elapsed.Stop();
            selected=selected! with{DurationMs=(DateTimeOffset.UtcNow-selected!.CreatedUtc).TotalMilliseconds,State=captureFailed?"Error de captura · audio parcial":"Grabada"};store.SaveMeeting(selected);
            RecordState(captureFailed?"Interrumpida · revisa el audio":"Reunión finalizada");
            bool liveFailed=false;
            if(liveTask!=null)
            {
                try{await liveTask;}
                catch(OperationCanceledException){liveFailed=true;Note("Transcripción cancelada. Puedes reanudarla desde Historial.");}
                catch(Exception ex){liveFailed=true;Error(ex);}
                finally{liveTask=null;processing?.Dispose();processing=null;}
            }
            if(!testMode&&settings.AutoProcess&&!captureFailed&&!liveFailed)await ProcessSelectedAsync(alreadyBusy:true);
            else SetStatus(captureFailed?"Captura interrumpida. Revisa el audio antes de procesar.":"Audio guardado. Escucha los dos canales y procesa cuando estés listo.");
        }
        finally{meetingActive=false;busy=false;LoadHistory();UpdateControls();}
    }
    private async Task ProcessSelectedAsync(bool alreadyBusy=false)
    {
        if(selected==null||meetingActive||(!alreadyBusy&&busy))return;
        if(!alreadyBusy)busy=true;
        var meeting=selected;
        processing=new();UpdateControls();
        try
        {
            await SaveSettingsAsync();settings.ValidateWhisper();
            store.SaveMeeting(meeting with{State="Transcribiendo",Error=null});
            var pipeline=NewPipeline();var token=processing.Token;var config=settings;var progress=ProcessingProgress();
            await Task.Run(()=>pipeline.TranscribeAsync(meeting,MeetingPipeline.DiscoverSources(meeting.Folder),config,progress,token),token);
            RefreshTranscript();
            store.SaveMeeting(meeting with{State="Analizando",Error=null});
            using var ollama=new OllamaAnalyzer(config);
            var segments=store.GetSegments(meeting.Id);
            var report=await ollama.AnalyzeAsync(meeting,segments,progress,token);
            store.SaveReport(meeting.Id,report);
            await ReportExporter.ExportAsync(meeting,report,segments,token);
            store.SaveMeeting(meeting with{State="Informe listo · borrador",Error=null});ShowReport(report);
            SetStatus("Informe ejecutivo generado localmente. Revisa el borrador antes de compartir.");
        }
        catch(OperationCanceledException){store.SaveMeeting(meeting with{State="IA cancelada · reanudable"});SetStatus("Procesamiento cancelado. El audio y los segmentos completados se conservan.");}
        catch(Exception ex){store.SaveMeeting(meeting with{State="IA pendiente · revisar configuración",Error=ex.Message});Error(ex);}
        finally
        {
            processing.Dispose();processing=null;
            if(!alreadyBusy)busy=false;
            RefreshTranscript();LoadHistory();UpdateControls();
        }
    }
    private void RefreshTranscript()
    {
        rows.Clear();if(selected==null)return;
        foreach(var segment in store.GetSegments(selected.Id))rows.Add(new(segment));
        TranscriptState.Text=$"{rows.Count} segmentos · original conservado en SQLite";
    }
    private void LoadHistory()
    {
        loadingHistory=true;
        try{var list=store.ListMeetings();HistoryGrid.ItemsSource=list;HistoryGrid.SelectedItem=list.FirstOrDefault(m=>m.Id==selected?.Id);}
        finally{loadingHistory=false;}
    }
    private void HistorySelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(loadingHistory||busy||meetingActive||HistoryGrid.SelectedItem is not Meeting meeting)return;
        StopPlayback();selected=meeting;MeetingTitle.Text=meeting.Title;Clock.Text=MeetingTime.Format(meeting.DurationMs);
        RefreshTranscript();ShowReport(store.GetReport(meeting.Id));SetStatus(meeting.Error??meeting.State);UpdateControls();
    }
    private void ShowReport(ExecutiveReport? report)
    {
        TopicCount.Text=report?.Topics.Length.ToString()??"—";DecisionCount.Text=report?.Decisions.Length.ToString()??"—";ActionCount.Text=report?.Actions.Length.ToString()??"—";
        ReportSummary.Text=report?.Summary??"El informe aparecerá aquí cuando finalice el análisis local. Configura Whisper.cpp y Ollama en Ajustes para comenzar.";
        ReportStatus.Text=report?.Status??"Sin informe generado";
        ReportDetails.Text=report==null?"Cada conclusión incluirá referencias a la transcripción. Los responsables y fechas no confirmados se indicarán como pendientes.":
            string.Join("\n\n",report.Decisions.Select(p=>"• "+p.Title+": "+p.Detail).Concat(report.Actions.Select(a=>"→ "+a.Task+" · "+(a.Owner??"Responsable por confirmar")+" · "+(a.DueDate??"Fecha por confirmar"))));
        OpenReport.IsEnabled=report!=null;
    }
    private async void Tick(object? sender,EventArgs e)
    {
        if(!meetingActive||busy)return;
        Clock.Text=MeetingTime.Format(elapsed.Elapsed.TotalMilliseconds);
        LevelA.Value=Volatile.Read(ref peakA);LevelB.Value=Volatile.Read(ref peakB);
        Volatile.Write(ref peakA,peakA*0.6f);Volatile.Write(ref peakB,peakB*0.6f);
        if(testMode&&elapsed.Elapsed>=TimeSpan.FromSeconds(30)){await Guard(()=>EndAsync());return;}
        if(session==null||++poll%8!=0)return;
        try
        {
            if(!Devices.List(DataFlow.Render).Any(d=>d.Id==outputId)||!Devices.List(DataFlow.Capture).Any(d=>d.Id==inputId))
            {Note("Dispositivo desconectado. No se cambiará de fuente automáticamente.");await EndAsync(captureFailed:true);}
        }catch(Exception ex){Error(ex);await Guard(()=>EndAsync(captureFailed:true));}
    }
    private async void PauseClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(busy||!meetingActive)return;busy=true;UpdateControls();
        try
        {
            if(session!=null){await StopTakeAsync();elapsed.Stop();RecordState("En pausa");store.SaveMeeting(selected! with{State="Pausada"});}
            else{await BeginTakeAsync();elapsed.Start();RecordState("Grabando dos fuentes");store.SaveMeeting(selected! with{State="Grabando"});}
        }finally{busy=false;UpdateControls();}
    });
    private void Play(string channel)
    {
        if(busy||meetingActive||selected==null)return;StopPlayback();
        var parts=Directory.GetFiles(selected.Folder,$"channel-{channel}*.wav",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Note($"Reproduciendo canal {channel}: {parts.Length} parte(s), por la salida predeterminada de Windows.");PlayPart(parts,0,playbackVersion);
    }
    private void PlayPart(string[] parts,int index,int version)
    {
        if(version!=playbackVersion||index>=parts.Length)return;
        try
        {
            reader=new AudioFileReader(parts[index]);var output=new WaveOutEvent();player=output;output.Init(reader);
            output.PlaybackStopped+=(_,args)=>Dispatcher.BeginInvoke(new Action(()=>
            {
                if(version!=playbackVersion||player!=output)return;
                player=null;output.Dispose();reader?.Dispose();reader=null;
                if(args.Exception!=null){Error(args.Exception);return;}PlayPart(parts,index+1,version);
            }));output.Play();
        }catch(Exception ex){StopPlayback();Error(ex);}
    }
    private void StopPlayback(){playbackVersion++;player?.Stop();player?.Dispose();player=null;reader?.Dispose();reader=null;}
    private async void ArchiveClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(busy||meetingActive||selected==null)return;busy=true;UpdateControls();StopPlayback();
        try
        {
            var progress=new Progress<double>(value=>SetStatus($"Guardando paquete de reunión: {value:P0}"));
            await ReportExporter.ExportTranscriptAsync(selected,store.GetSegments(selected.Id),CancellationToken.None);
            var report=store.GetReport(selected.Id);
            if(report!=null)await ReportExporter.ExportAsync(selected,report,store.GetSegments(selected.Id),CancellationToken.None);
            await ReportExporter.AtomicWriteAsync(Path.Combine(selected.Folder,"meeting-info.json"),JsonSerializer.Serialize(new{selected.Id,selected.Title,selected.CreatedUtc,selected.DurationMs},JsonDefaults.Options),CancellationToken.None);
            var path=await MeetingArchive.CreateAsync(selected.Folder,progress);SetStatus("Paquete guardado con audio, marcas de tiempo y resultados disponibles.");Note(path);
        }finally{busy=false;UpdateControls();}
    });
    private async void SaveTextClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(busy||meetingActive||selected==null)return;
        TranscriptGrid.CommitEdit(DataGridEditingUnit.Cell,true);TranscriptGrid.CommitEdit(DataGridEditingUnit.Row,true);
        store.SaveCorrections(selected.Id,rows.ToDictionary(r=>r.Id,r=>r.Text));
        await ReportExporter.ExportTranscriptAsync(selected,store.GetSegments(selected.Id),CancellationToken.None);
        ShowReport(store.GetReport(selected.Id));SetStatus("Correcciones guardadas. Regenera el informe para incorporar cambios.");
    });
    private async void ImportClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(busy||meetingActive)return;
        var dialog=new OpenFileDialog{Filter="Audio compatible|*.wav;*.mp3;*.flac;*.m4a|WAV|*.wav"};if(dialog.ShowDialog()!=true)return;
        busy=true;UpdateControls();
        try
        {
            var folder=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-import-"+Guid.NewGuid().ToString("N")[..6]);Directory.CreateDirectory(folder);
            SetStatus("Copiando audio importado…");
            var destination=Path.Combine(folder,"imported"+Path.GetExtension(dialog.FileName).ToLowerInvariant());
            await Task.Run(()=>File.Copy(dialog.FileName,destination));
            selected=new(Guid.NewGuid().ToString("N"),folder,Path.GetFileNameWithoutExtension(dialog.FileName),DateTimeOffset.UtcNow);
            store.SaveMeeting(selected);MeetingTitle.Text=selected.Title;RefreshTranscript();ShowReport(null);SetStatus("Audio importado. Pulsa Transcribir y generar informe.");NavigateTo(1);
        }finally{busy=false;LoadHistory();UpdateControls();}
    });
    private async void OpenReportClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(selected==null)return;var report=store.GetReport(selected.Id);if(report==null)return;
        await ReportExporter.ExportAsync(selected,report,store.GetSegments(selected.Id),CancellationToken.None);
        OpenUrl(Path.Combine(selected.Folder,"report.html"));
    });
    private async void CheckEnginesClick(object sender,RoutedEventArgs e)=>await Guard(async()=>
    {
        if(busy||meetingActive)return;busy=true;UpdateControls();
        try
        {
            await SaveSettingsAsync();settings.ValidateWhisper();using var ollama=new OllamaAnalyzer(settings);
            EngineStatus.Text="Comprobando Ollama local…";
            await ollama.CheckAsync(CancellationToken.None);
            EngineStatus.Text="Archivos de Whisper presentes y modelo de Ollama local disponible. La primera transcripción verificará la compatibilidad del ejecutable.";
            EngineBadge.Text="● Ollama local disponible";
        }catch(Exception ex){EngineStatus.Text=ex.Message;throw;}
        finally{busy=false;UpdateControls();}
    });
    private async void SaveSettingsClick(object sender,RoutedEventArgs e)=>await Guard(async()=>{await SaveSettingsAsync();EngineStatus.Text="Ajustes guardados. Comprueba la configuración antes de procesar.";});
    private void BrowseWhisperClick(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Whisper CLI|*.exe"};if(dialog.ShowDialog()==true)WhisperPath.Text=dialog.FileName;}
    private void BrowseModelClick(object sender,RoutedEventArgs e){var dialog=new OpenFileDialog{Filter="Modelo Whisper|*.bin"};if(dialog.ShowDialog()==true)ModelPath.Text=dialog.FileName;}
    private void OpenUrl(string url){try{System.Diagnostics.Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){Error(ex);}}
    private void WhisperDownloadsClick(object sender,RoutedEventArgs e)=>OpenUrl("https://github.com/ggml-org/whisper.cpp/releases");
    private void OllamaDownloadsClick(object sender,RoutedEventArgs e)=>OpenUrl("https://ollama.com/download/windows");
    private void CancelAIClick(object sender,RoutedEventArgs e){processing?.Cancel();SetStatus("Cancelando IA. La captura de audio continúa si la reunión sigue activa.");UpdateControls();}
    private async void TestClick(object sender,RoutedEventArgs e)=>await Guard(()=>StartAsync(true));
    private async void RecordClick(object sender,RoutedEventArgs e)=>await Guard(()=>StartAsync(false));
    private async void StopClick(object sender,RoutedEventArgs e)=>await Guard(()=>EndAsync());
    private async void ProcessClick(object sender,RoutedEventArgs e)=>await Guard(()=>ProcessSelectedAsync());
    private void RefreshClick(object sender,RoutedEventArgs e)=>RefreshDevices();
    private void PlayAClick(object sender,RoutedEventArgs e)=>Play("A");
    private void PlayBClick(object sender,RoutedEventArgs e)=>Play("B");
    private void StopPlaybackClick(object sender,RoutedEventArgs e)=>StopPlayback();
    private void OpenFolderClick(object sender,RoutedEventArgs e){Directory.CreateDirectory(root);OpenUrl(selected?.Folder??root);}
    private async void OnClosing(object? sender,CancelEventArgs e)
    {
        if(closing)return;e.Cancel=true;
        if(busy){SetStatus("Cancela o espera el procesamiento antes de cerrar.");return;}
        if(meetingActive)
        {
            // Closing saves capture and checkpoints; lengthy analysis can be resumed from History.
            processing?.Cancel();var auto=settings.AutoProcess;settings=settings with{AutoProcess=false};
            await Guard(()=>EndAsync());settings=settings with{AutoProcess=auto};
        }
        StopPlayback();timer.Stop();processing?.Cancel();closing=true;Close();
    }
    public sealed class SegmentRow(TranscriptSegment segment)
    {
        public string Id {get;}=segment.Id;
        public string Time {get;}=MeetingTime.Format(segment.StartMs);
        public string Channel {get;}=segment.Channel;
        public string Text {get;set;}=segment.Text;
    }
}
