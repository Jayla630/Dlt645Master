using Dlt645Master.App.Services;

namespace Dlt645Master.App.Tests.Fakes;

/// <summary>同步执行的 <see cref="IUiDispatcher"/> 替身：直接调用 action，并记录 Post 次数供断言。</summary>
public sealed class SyncUiDispatcher : IUiDispatcher
{
    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        PostCount++;
        action();
    }
}
