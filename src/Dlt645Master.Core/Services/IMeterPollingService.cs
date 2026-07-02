using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Services;

/// <summary>
/// 电表轮询服务：封装「一问一答」与周期轮询。服务不负责连接生命周期——
/// 调用方须在 <see cref="Start"/> 前自行 Open 传输层，Start 时若传输层未打开将抛异常。
/// 注意：<see cref="ReadCompleted"/>、<see cref="FrameTransferred"/>、<see cref="StatisticsChanged"/>
/// 均在后台工作线程上触发，订阅方（视图模型）必须自行调度回 UI 线程。
/// </summary>
public interface IMeterPollingService : IDisposable
{
    bool IsPolling { get; }

    /// <summary>
    /// 启动周期轮询。传输层未打开（IsOpen == false）时抛 <see cref="InvalidOperationException"/>。
    /// 轮询已在运行时重复调用直接抛 <see cref="InvalidOperationException"/>（不做幂等静默忽略）。
    /// </summary>
    void Start(PollingOptions options);

    /// <summary>停止轮询并等待后台工作循环退出（阻塞至退出或短暂宽限期超时）。未在轮询时调用为空操作，可重复调用。</summary>
    void Stop();

    /// <summary>
    /// 单次读取（同步阻塞，供「单次读取」按钮或测试用）。
    /// 超时返回失败的 <see cref="MeterReadResult"/>（IsSuccess=false，ErrorMessage 注明超时），不抛异常。
    /// 轮询运行期间调用会与轮询循环争抢半双工总线，直接抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    MeterReadResult ReadOnce(byte[] meterAddress, byte[] dataId, TimeSpan timeout);

    event EventHandler<MeterReadResultEventArgs>? ReadCompleted;

    event EventHandler<FrameTransferredEventArgs>? FrameTransferred;

    event EventHandler<StatisticsChangedEventArgs>? StatisticsChanged;

    /// <summary>当前统计快照（线程安全，可随时读取）。</summary>
    CommStatistics Statistics { get; }
}
