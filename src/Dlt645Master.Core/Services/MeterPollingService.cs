using System.Diagnostics;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Transport;

namespace Dlt645Master.Core.Services;

/// <summary>
/// <see cref="IMeterPollingService"/> 的实现：单个后台工作循环 + 同步一问一答，
/// 不引入 async 接口 / Rx / Channel（半双工总线一次只允许一问一答在飞，没有并发需求）。
/// 事件在后台工作线程上触发，订阅方须自行调度回 UI 线程。
/// </summary>
public sealed class MeterPollingService : IMeterPollingService
{
    private readonly ITransport _transport;
    private readonly IMeterProtocol _protocol;

    /// <summary>半双工总线互斥锁：一问一答（无论来自 ReadOnce 还是轮询循环）全程持有。</summary>
    private readonly object _busLock = new();

    private long _txFrameCount;
    private long _rxFrameCount;
    private long _timeoutCount;
    private long _errorCount;
    private double _lastRoundTripMs;

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public MeterPollingService(ITransport transport, IMeterProtocol protocol)
    {
        _transport = transport;
        _protocol = protocol;
    }

    public bool IsPolling { get; private set; }

    public event EventHandler<MeterReadResultEventArgs>? ReadCompleted;

    public event EventHandler<FrameTransferredEventArgs>? FrameTransferred;

    public event EventHandler<StatisticsChangedEventArgs>? StatisticsChanged;

    public CommStatistics Statistics => new()
    {
        TxFrameCount = Interlocked.Read(ref _txFrameCount),
        RxFrameCount = Interlocked.Read(ref _rxFrameCount),
        TimeoutCount = Interlocked.Read(ref _timeoutCount),
        ErrorCount = Interlocked.Read(ref _errorCount),
        LastRoundTripMs = Volatile.Read(ref _lastRoundTripMs),
    };

    public void Start(PollingOptions options)
    {
        if (!_transport.IsOpen)
        {
            throw new InvalidOperationException("传输层尚未打开（IsOpen == false），服务不负责连接生命周期，请先由调用方 Open。");
        }

        if (IsPolling)
        {
            throw new InvalidOperationException("轮询已在运行中，不允许重复 Start。");
        }

        _pollingCts = new CancellationTokenSource();
        CancellationToken token = _pollingCts.Token;
        IsPolling = true;
        _pollingTask = Task.Run(() => PollingLoop(options, token), CancellationToken.None);
    }

    public void Stop()
    {
        if (_pollingCts is null)
        {
            return;
        }

        _pollingCts.Cancel();
        try
        {
            _pollingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // 循环内部已自行兜底所有异常，这里只是防御性忽略取消引发的收尾异常。
        }

        _pollingCts.Dispose();
        _pollingCts = null;
        _pollingTask = null;
        IsPolling = false;
    }

    public MeterReadResult ReadOnce(byte[] meterAddress, byte[] dataId, TimeSpan timeout)
    {
        if (IsPolling)
        {
            throw new InvalidOperationException("轮询运行期间不允许调用 ReadOnce（半双工总线不允许并发一问一答）。");
        }

        return Exchange(meterAddress, dataId, timeout);
    }

    private void PollingLoop(PollingOptions options, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (byte[] dataId in options.DataIds)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        Exchange(options.MeterAddress, dataId, options.ResponseTimeout);
                    }
                    catch
                    {
                        // 单次异常不得让轮询循环悄然死掉；已在 Exchange 内部记入 ErrorCount，这里只吞掉异常继续下一项。
                    }

                    if (token.WaitHandle.WaitOne(options.InterFrameDelay))
                    {
                        break;
                    }
                }

                if (token.WaitHandle.WaitOne(options.PollInterval))
                {
                    break;
                }
            }
        }
        catch
        {
            // 兜底：循环级别的任何未预期异常都不应该向上抛出导致后台任务悄然终止。
        }
    }

    /// <summary>一问一答核心：Start 循环与 ReadOnce 共用。全程持有 _busLock，避免与并发调用争抢半双工总线。</summary>
    private MeterReadResult Exchange(byte[] meterAddress, byte[] dataId, TimeSpan timeout)
    {
        lock (_busLock)
        {
            byte[] requestFrame = _protocol.BuildReadRequest(meterAddress, dataId);
            var stopwatch = Stopwatch.StartNew();

            _transport.Send(requestFrame);
            Interlocked.Increment(ref _txFrameCount);
            RaiseFrameTransferred(FrameDirection.Tx, requestFrame);

            byte[]? responseFrame = _transport.ReceiveFrame(timeout);
            if (responseFrame is null)
            {
                Interlocked.Increment(ref _timeoutCount);
                RaiseStatisticsChanged();

                MeterReadResult timeoutResult = MeterReadResult.Failure("等待应答超时");
                RaiseReadCompleted(timeoutResult);
                return timeoutResult;
            }

            Interlocked.Increment(ref _rxFrameCount);
            RaiseFrameTransferred(FrameDirection.Rx, responseFrame);

            MeterReadResult result = _protocol.TryParseResponse(responseFrame);
            if (result.IsSuccess)
            {
                Volatile.Write(ref _lastRoundTripMs, stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                Interlocked.Increment(ref _errorCount);
            }

            RaiseStatisticsChanged();
            RaiseReadCompleted(result);
            return result;
        }
    }

    private void RaiseReadCompleted(MeterReadResult result)
    {
        ReadCompleted?.Invoke(this, new MeterReadResultEventArgs { Result = result, Timestamp = DateTimeOffset.Now });
    }

    private void RaiseFrameTransferred(FrameDirection direction, byte[] frame)
    {
        FrameTransferred?.Invoke(this, new FrameTransferredEventArgs
        {
            Direction = direction,
            Frame = (byte[])frame.Clone(),
            Timestamp = DateTimeOffset.Now,
        });
    }

    private void RaiseStatisticsChanged()
    {
        StatisticsChanged?.Invoke(this, new StatisticsChangedEventArgs { Snapshot = Statistics });
    }

    public void Dispose()
    {
        Stop();
    }
}
