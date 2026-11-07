using System.IO;
using System.Windows;
namespace LRMeeting.App;
public partial class App:Application
{
    private Mutex? instance;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if(!e.Args.Contains("--smoke-test"))
            {
                instance=new Mutex(true,@"Local\LRMeetingAI",out ownsInstance);
                if(!ownsInstance){MessageBox.Show("LR Meeting AI ya está abierto.","LR Meeting AI");Shutdown();return;}
            }
            new MainWindow().Show();
        }
        catch(Exception ex)
        {
            var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LR Meeting AI");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"startup-error.log"),ex.ToString());
            if(!e.Args.Contains("--smoke-test"))MessageBox.Show("No se pudo abrir LR Meeting AI: "+ex.Message,"LR Meeting AI");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e){if(ownsInstance)instance?.ReleaseMutex();instance?.Dispose();base.OnExit(e);}
}
