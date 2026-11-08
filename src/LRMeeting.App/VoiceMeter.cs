using System.Windows;
using System.Windows.Media;
namespace LRMeeting.App;

// A scrolling history of measured amplitude, not a fabricated frequency spectrum.
public sealed class VoiceMeter : FrameworkElement
{
    private readonly double[] samples = new double[64];
    public Brush Accent { get; set; } = Brushes.Turquoise;
    public void Push(double peak)
    {
        Array.Copy(samples, 1, samples, 0, samples.Length - 1);
        samples[^1] = double.IsFinite(peak) ? Math.Clamp(peak, 0, 1) : 0;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if(ActualWidth<=0||ActualHeight<=0)return;
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(40,200,225,210)),1),new Point(0,ActualHeight/2),new Point(ActualWidth,ActualHeight/2));
        var step = ActualWidth / samples.Length;
        for (var i = 0; i < samples.Length; i++)
        {
            var height = Math.Max(3, Math.Sqrt(samples[i]) * Math.Max(0, ActualHeight - 4));
            context.PushOpacity(samples[i] > 0.002 ? 1 : 0.22);
            context.DrawRoundedRectangle(Accent, null,
                new Rect(i * step, (ActualHeight - height) / 2, Math.Max(1, step * 0.55), height), 2, 2);
            context.Pop();
        }
    }
}
