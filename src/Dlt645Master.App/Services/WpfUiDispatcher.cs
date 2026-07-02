using System.Windows;

namespace Dlt645Master.App.Services;

/// <summary>
/// <see cref="IUiDispatcher"/> 的 WPF 实现：经 <see cref="System.Windows.Threading.Dispatcher.BeginInvoke(Delegate, object[])"/>
/// 异步调度回界面线程。没有 <see cref="Application.Current"/> 时（设计期/极端场景）退化为直接执行，避免抛空引用。
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        Application? app = Application.Current;
        if (app is null)
        {
            action();
            return;
        }

        app.Dispatcher.BeginInvoke(action);
    }
}
