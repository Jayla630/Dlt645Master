using System.Windows;

namespace Dlt645Master.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 单窗口应用：窗口关闭即退出。顺手让视图模型退订服务事件并释放服务/传输（其实现 IDisposable）。
        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }
}
