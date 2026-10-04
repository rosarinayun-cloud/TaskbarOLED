// Defaults: idle seconds, black overlay opacity (0..100), mouse polling milliseconds.
// Edit TaskbarOLED.ini beside the EXE to change these without rebuilding.
using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; public Rectangle Box { get { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } } }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT monitor, work; public uint flags; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder name, int max);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFO m);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT value, int bytes);
    public static string Class(IntPtr h) { var b=new StringBuilder(256); GetClassName(h,b,b.Capacity); return b.ToString(); }
}

internal sealed class Dimmer : Form {
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly EventWaitHandle stop;
    readonly NotifyIcon tray;
    readonly int idleSeconds, blackPercent;
    long lastInteraction;
    bool dimmed, paused;
    Rectangle previous;
    string state = "";
    readonly string logPath;
    public Dimmer(EventWaitHandle stopEvent, int idle, int black, int poll, bool diagnostics) {
        stop=stopEvent; idleSeconds=idle; blackPercent=black;
        if (diagnostics) logPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification.log");
        Text="Taskbar OLED Overlay";
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; BackColor=Color.Black;
        AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual;
        Bounds=new Rectangle(-32000,-32000,1,1);
        var handle=Handle;
        if (!Native.SetLayeredWindowAttributes(handle,0,(byte)Math.Round(255.0*blackPercent/100),2)) throw new System.ComponentModel.Win32Exception();
        bool hotkey=Native.RegisterHotKey(handle,1,0x4000|1|2|4,0x4F);
        var menu=new ContextMenuStrip();
        var pauseItem=new ToolStripMenuItem("Pause / resume");
        pauseItem.Click += delegate { paused=!paused; pauseItem.Checked=paused; lastInteraction=clock.ElapsedMilliseconds; SetDim(false,"paused",Rectangle.Empty); };
        menu.Items.Add(pauseItem);
        menu.Items.Add("Exit (Ctrl+Alt+Shift+O)",null,delegate { Quit(); });
        tray=new NotifyIcon { Icon=SystemIcons.Application, Text="Taskbar OLED - 60s idle", ContextMenuStrip=menu, Visible=true };
        tray.Text="Taskbar OLED - "+idle+"s idle";
        timer.Interval=poll; timer.Tick += Tick; timer.Start();
        Log("START idle="+idle+" black="+black+" poll="+poll+" hotkey="+hotkey+" hwnd="+handle);
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle |= 0x80000|0x20|0x08000000|0x80; return p; } }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x84) { m.Result=new IntPtr(-1); return; }
        if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
        if(m.Msg==0x10 || (m.Msg==0x312 && m.WParam.ToInt32()==1)) { Quit(); return; }
        base.WndProc(ref m);
    }
    void Quit() { timer.Stop(); Native.ShowWindow(Handle,0); Application.ExitThread(); }
    void Log(string line) { if(logPath!=null) try { File.AppendAllText(logPath,DateTime.Now.ToString("O")+" "+clock.ElapsedMilliseconds+"ms "+line+Environment.NewLine); } catch {} }
    void SetDim(bool show,string why,Rectangle box) {
        if(show) {
            if(!dimmed || box!=previous) {
                if(!Native.SetWindowPos(Handle,new IntPtr(-1),box.X,box.Y,box.Width,box.Height,0x10|0x40|0x200)) {
                    Native.ShowWindow(Handle,0); dimmed=false; Log("POSITION FAILED"); return;
                }
            }
        } else if(dimmed) Native.ShowWindow(Handle,0);
        if(dimmed!=show || state!=why) Log((show?"DIM ":"CLEAR ")+why+" rect="+box+" foreground="+Native.GetForegroundWindow());
        dimmed=show; state=why; previous=box;
    }
    bool ReadTaskbar(out IntPtr taskbar,out Rectangle box,out Rectangle monitor) {
        taskbar=Native.FindWindow("Shell_TrayWnd",null); box=monitor=Rectangle.Empty;
        Native.RECT r;
        if(taskbar==IntPtr.Zero || !Native.IsWindowVisible(taskbar) || !Native.GetWindowRect(taskbar,out r)) return false;
        var mi=new Native.MONITORINFO(); mi.cbSize=Marshal.SizeOf(mi);
        if(!Native.GetMonitorInfo(Native.MonitorFromWindow(taskbar,2),ref mi)) return false;
        monitor=mi.monitor.Box; box=Rectangle.Intersect(r.Box,monitor);
        // Never shade an off-screen/auto-hidden sliver or an implausibly large rectangle.
        return box.Width>4 && box.Height>4 && (box.Height<=monitor.Height/4 || box.Width<=monitor.Width/4);
    }
    bool Covered(IntPtr foreground, IntPtr taskbar, Rectangle box, Rectangle monitor) {
        if(foreground==IntPtr.Zero || foreground==Handle || foreground==taskbar) return false;
        string cls=Native.Class(foreground);
        if(cls=="Progman" || cls=="WorkerW" || cls=="Shell_TrayWnd" || cls=="Shell_SecondaryTrayWnd") return false;
        Native.RECT r;
        if(!Native.GetWindowRect(foreground,out r)) return true;
        Rectangle full=r.Box;
        if(full.Left<=monitor.Left && full.Top<=monitor.Top && full.Right>=monitor.Right && full.Bottom>=monitor.Bottom) return true;
        if(Native.DwmGetWindowAttribute(foreground,9,out r,Marshal.SizeOf(typeof(Native.RECT)))==0) full=r.Box;
        Rectangle intersection=Rectangle.Intersect(full,box);
        return intersection.Width>2 && intersection.Height>2;
    }
    bool SurfaceCovered(IntPtr taskbar, Rectangle box) {
        // Hit-testing passes through our layered transparent window. This also catches
        // topmost fullscreen/ordinary windows even when Windows denies them foreground focus.
        int[] xs={box.Left+Math.Min(20,box.Width/2),box.Left+box.Width/2,box.Right-Math.Min(20,box.Width/2)};
        foreach(int x in xs) {
            var point=new Native.POINT { X=x, Y=box.Top+box.Height/2 };
            IntPtr target=Native.GetAncestor(Native.WindowFromPoint(point),2);
            if(target!=IntPtr.Zero && target!=taskbar && target!=Handle) return true;
        }
        return false;
    }
    void Tick(object sender,EventArgs args) {
        try {
            if(stop.WaitOne(0)) { Quit(); return; }
            IntPtr taskbar; Rectangle box,monitor;
            if(!ReadTaskbar(out taskbar,out box,out monitor)) { lastInteraction=clock.ElapsedMilliseconds; SetDim(false,"taskbar-unavailable",box); return; }
            Native.POINT point;
            if(!Native.GetCursorPos(out point)) { SetDim(false,"cursor-unavailable",box); return; }
            IntPtr foreground=Native.GetForegroundWindow();
            bool interaction=box.Contains(point.X,point.Y) || Native.GetAncestor(foreground,2)==taskbar;
            if(interaction) lastInteraction=clock.ElapsedMilliseconds;
            if(interaction) SetDim(false,"taskbar-interaction",box);
            else if(paused) SetDim(false,"paused",box);
            else if(Covered(foreground,taskbar,box,monitor) || SurfaceCovered(taskbar,box)) { lastInteraction=clock.ElapsedMilliseconds; SetDim(false,"fullscreen-or-covered",box); }
            else if(clock.ElapsedMilliseconds-lastInteraction>=idleSeconds*1000L) SetDim(true,"idle",box);
            else SetDim(false,"countdown",box);
        } catch(Exception ex) { SetDim(false,"error",Rectangle.Empty); Log(ex.ToString()); }
    }
    protected override void Dispose(bool disposing) {
        if(disposing) {
            timer.Stop(); timer.Dispose(); Native.ShowWindow(Handle,0); Native.UnregisterHotKey(Handle,1);
            if(tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); }
            Log("EXIT overlay-hidden");
        }
        base.Dispose(disposing);
    }
}

internal static class Program {
    const string InstanceName="Local\\TaskbarOLED.Study.60s.v1";
    static int Read(string key,int fallback,int min,int max) {
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TaskbarOLED.ini");
        if(File.Exists(path)) foreach(string line in File.ReadAllLines(path)) {
            var pair=line.Split('='); int n;
            if(pair.Length==2 && pair[0].Trim().Equals(key,StringComparison.OrdinalIgnoreCase) && int.TryParse(pair[1].Trim(),out n)) return Math.Max(min,Math.Min(max,n));
        }
        return fallback;
    }
    [STAThread] public static void Main(string[] args) {
        if(Array.IndexOf(args,"--stop")>=0) { try { using(var e=EventWaitHandle.OpenExisting(InstanceName+".Stop")) e.Set(); } catch(WaitHandleCannotBeOpenedException) {} return; }
        bool created;
        using(var mutex=new Mutex(true,InstanceName,out created)) {
            if(!created) return;
            try {
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException) { Native.SetProcessDPIAware(); }
                using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,InstanceName+".Stop")) {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using(var form=new Dimmer(stop,Read("IdleSeconds",60,1,86400),Read("BlackPercent",70,1,95),Read("PollMilliseconds",100,50,200),Array.IndexOf(args,"--diagnostics")>=0)) {
                        form.FormClosed += delegate { Application.ExitThread(); };
                        Application.Run();
                    }
                }
            } catch(Exception ex) { MessageBox.Show(ex.Message,"Taskbar OLED",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
