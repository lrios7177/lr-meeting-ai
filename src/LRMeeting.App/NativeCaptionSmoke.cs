using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;

namespace LRMeeting.App;

/// <summary>Exercises the actual Windows caption through its native automation provider.</summary>
internal static class NativeCaptionSmoke
{
    internal const int Restore=0xF120,Close=0xF060;
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong(nint handle,int index);
    [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint handle,uint message,nint command,nint parameter);

    internal static void VerifyNativeStyles(Window window)
    {
        const int required=0x00C00000|0x00080000|0x00020000|0x00010000;
        if((GetWindowLong(new WindowInteropHelper(window).Handle,-16)&required)!=required)
            throw new InvalidDataException("Windows caption, system menu and minimize/maximize boxes are required.");
    }

    internal static Task InvokeAsync(Window window,string automationId)
    {
        var handle=new WindowInteropHelper(window).Handle;
        // UI Automation must run off the WPF dispatcher to avoid blocking its message pump.
        return Task.Run(()=>
        {
            var element=AutomationElement.FromHandle(handle);
            var button=element.FindFirst(TreeScope.Descendants,
                new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty,automationId),
                                 new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)))
                ??throw new InvalidDataException($"Windows did not expose its native {automationId} caption button.");
            var rectangle=button.Current.BoundingRectangle;
            if(rectangle.IsEmpty||rectangle.Width<=0||rectangle.Height<=0||!button.Current.IsEnabled)
                throw new InvalidDataException($"Native {automationId} caption button is not available.");
            if(!button.TryGetCurrentPattern(InvokePattern.Pattern,out var pattern))
                throw new InvalidDataException($"Native {automationId} does not support InvokePattern.");
            ((InvokePattern)pattern).Invoke();
        });
    }

    internal static void Post(Window window,int command)
    {
        if(!PostMessage(new WindowInteropHelper(window).Handle,0x0112,command,0))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
}
