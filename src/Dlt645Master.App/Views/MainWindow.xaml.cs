using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Dlt645Master.App.ViewModels;

namespace Dlt645Master.App.Views;

public partial class MainWindow : Window
{
    private INotifyCollectionChanged? _frameLog;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 报文监视自动滚到底：监听集合变化滚动到末项（纯视图行为，留在代码隐藏而不进视图模型）。
        if (_frameLog is null && DataContext is MainWindowViewModel viewModel)
        {
            _frameLog = viewModel.FrameLog;
            _frameLog.CollectionChanged += OnFrameLogChanged;
        }
    }

    private void OnFrameLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && FrameLogList.Items.Count > 0)
        {
            FrameLogList.ScrollIntoView(FrameLogList.Items[FrameLogList.Items.Count - 1]);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_frameLog is not null)
        {
            _frameLog.CollectionChanged -= OnFrameLogChanged;
            _frameLog = null;
        }

        // 单窗口应用：窗口关闭即退出。顺手让视图模型退订服务事件并释放服务/传输（其实现 IDisposable）。
        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }

    // ---- 窗控按钮（WindowChrome 标题栏）----

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    // ---- WM_GETMINMAXINFO：WindowStyle=None 下最大化默认占满整屏（盖住任务栏），
    // 在此把最大化位置/尺寸精确限制到窗口所在显示器的工作区。按当前显示器取工作区，多屏亦正确；
    // 位置与尺寸都由本钩子精确给定，因此无需「最大化态加 7px 内边距」的溢出修正。----

    /// <summary>钩子必须在窗口句柄创建时（首次显示与启动即最大化之前）挂上，初始 Maximized 才不会盖任务栏。</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmGetMinMaxInfo)
        {
            ClampMaximizedBoundsToWorkArea(hwnd, lParam);
        }

        return IntPtr.Zero; // handled 保持 false，让 WPF 继续按 MinWidth/MinHeight 填 ptMinTrackSize
    }

    private static void ClampMaximizedBoundsToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);

        // MINMAXINFO 使用相对显示器原点的物理像素坐标，工作区（rcWork）已扣除任务栏。
        NativeMethods.Rect work = monitorInfo.WorkArea;
        NativeMethods.Rect display = monitorInfo.MonitorArea;
        info.MaxPosition.X = work.Left - display.Left;
        info.MaxPosition.Y = work.Top - display.Top;
        info.MaxSize.X = work.Right - work.Left;
        info.MaxSize.Y = work.Bottom - work.Top;

        Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
    }

    /// <summary>Win32 互操作声明，仅本窗口使用。</summary>
    private static class NativeMethods
    {
        public const int WmGetMinMaxInfo = 0x0024;
        public const int MonitorDefaultToNearest = 0x00000002;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MinMaxInfo
        {
            public Point Reserved;
            public Point MaxSize;
            public Point MaxPosition;
            public Point MinTrackSize;
            public Point MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MonitorInfo
        {
            public int Size;
            public Rect MonitorArea;
            public Rect WorkArea;
            public int Flags;
        }
    }
}
