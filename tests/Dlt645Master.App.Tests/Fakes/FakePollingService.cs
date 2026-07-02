using Dlt645Master.Core.Models;
using Dlt645Master.Core.Services;

namespace Dlt645Master.App.Tests.Fakes;

/// <summary>
/// <see cref="IMeterPollingService"/> 的假实现：Start/Stop 维护 IsPolling 并计数，事件可手动触发
/// （模拟后台线程回调），ReadOnce 返回可设定结果。用于隔离视图模型逻辑与真实轮询/传输。
/// </summary>
public sealed class FakePollingService : IMeterPollingService
{
    public bool IsPolling { get; private set; }

    public int StartCallCount { get; private set; }

    public int StopCallCount { get; private set; }

    public int ReadOnceCallCount { get; private set; }

    public PollingOptions? LastStartOptions { get; private set; }

    public byte[]? LastReadOnceAddress { get; private set; }

    public byte[]? LastReadOnceDataId { get; private set; }

    /// <summary>为 true 时 <see cref="Start"/> 抛异常，用于验证启动命令的异常兜底。</summary>
    public bool ThrowOnStart { get; set; }

    /// <summary><see cref="ReadOnce"/> 的返回值，测试可预设。</summary>
    public MeterReadResult ReadOnceResult { get; set; } = MeterReadResult.Failure("未设定返回值");

    public CommStatistics Statistics { get; set; } = new();

    public event EventHandler<MeterReadResultEventArgs>? ReadCompleted;

    public event EventHandler<FrameTransferredEventArgs>? FrameTransferred;

    public event EventHandler<StatisticsChangedEventArgs>? StatisticsChanged;

    public void Start(PollingOptions options)
    {
        StartCallCount++;
        LastStartOptions = options;
        if (ThrowOnStart)
        {
            throw new InvalidOperationException("模拟启动轮询失败");
        }

        IsPolling = true;
    }

    public void Stop()
    {
        StopCallCount++;
        IsPolling = false;
    }

    public MeterReadResult ReadOnce(byte[] meterAddress, byte[] dataId, TimeSpan timeout)
    {
        ReadOnceCallCount++;
        LastReadOnceAddress = meterAddress;
        LastReadOnceDataId = dataId;
        return ReadOnceResult;
    }

    public void Dispose()
    {
    }

    public void RaiseReadCompleted(MeterReadResult result)
        => ReadCompleted?.Invoke(this, new MeterReadResultEventArgs { Result = result, Timestamp = DateTimeOffset.Now });

    public void RaiseFrameTransferred(FrameDirection direction, byte[] frame)
        => FrameTransferred?.Invoke(this, new FrameTransferredEventArgs { Direction = direction, Frame = frame, Timestamp = DateTimeOffset.Now });

    public void RaiseStatisticsChanged(CommStatistics snapshot)
        => StatisticsChanged?.Invoke(this, new StatisticsChangedEventArgs { Snapshot = snapshot });
}
