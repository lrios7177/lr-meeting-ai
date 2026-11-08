using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LRMeeting.App;

/// <summary>Smoke checks for the real Windows non-client caption, not custom WPF click handlers.</summary>
internal static class NativeCaptionSmoke
{
    internal const int Minimize=0xF020,Maximize=0xF030,Restore=0xF120,Close=0xF060;
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TitleBarInfo
    {
        public uint Size;
        public NativeRect Bounds;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=6)] public uint[] States;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=6)] public NativeRect[] Rectangles;
    }
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong(nint handle,int index);
    [DllImport("user32.dll",EntryPoint="SendMessageW")]
    private static extern nint GetTitleBar(nint handle,uint message,nint parameter,ref TitleBarInfo info);
    [DllImport("user32.dll",EntryPoint="SendMessageW")]
    private static extern nint SendMessage(nint handle,uint message,nint parameter,nint point);
    [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint handle,uint message,nint command,nint parameter);

    internal static void VerifyHitTests(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        const int required=0x00C00000|0x00080000|0x00020000|0x00010000;
        if((GetWindowLong(handle,-16)&required)!=required)
            throw new InvalidDataException("Windows caption, system menu and minimize/maximize boxes are required.");
        var info=new TitleBarInfo{Size=(uint)Marshal.SizeOf<TitleBarInfo>(),States=new uint[6],Rectangles=new NativeRect[6]};
        GetTitleBar(handle,0x033F,0,ref info); // WM_GETTITLEBARINFOEX
        foreach(var (index,expected) in new[]{(2,8),(3,9),(5,20)})
        {
            var bounds=info.Rectangles[index];
            if(bounds.Right<=bounds.Left||bounds.Bottom<=bounds.Top)
                throw new InvalidDataException($"Native caption button {index} has no clickable bounds.");
            int x=(bounds.Left+bounds.Right)/2,y=(bounds.Top+bounds.Bottom)/2;
            var point=unchecked((nint)((y<<16)|(x&0xFFFF)));
            int actual=(int)SendMessage(handle,0x0084,0,point); // WM_NCHITTEST
            if(actual!=expected)throw new InvalidDataException($"Caption hit-test {index}: expected {expected}, received {actual}.");
        }
    }

    internal static void Post(Window window,int command)
    {
        if(!PostMessage(new WindowInteropHelper(window).Handle,0x0112,command,0)) // WM_SYSCOMMAND
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
}
