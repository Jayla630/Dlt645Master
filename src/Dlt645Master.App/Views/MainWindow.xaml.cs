using System.Collections.Specialized;
using System.Windows;
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
}
