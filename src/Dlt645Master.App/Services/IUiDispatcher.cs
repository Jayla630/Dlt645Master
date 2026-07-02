namespace Dlt645Master.App.Services;

/// <summary>
/// 界面线程调度抽象。轮询服务的三个事件都在后台工作线程触发，视图模型不能直接改
/// <c>ObservableCollection</c>，必须经本抽象调度回界面线程。抽象化的目的还在于让视图模型可单元测试
/// （测试环境没有 WPF <c>Dispatcher</c>，用同步执行的替身即可）。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>把 <paramref name="action"/> 调度到界面线程执行。</summary>
    void Post(Action action);
}
