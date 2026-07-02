using System.Collections.ObjectModel;
using System.ComponentModel;
using Dlt645Master.App.Configuration;
using Dlt645Master.App.Services;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Services;
using Dlt645Master.Core.Transport;
using Prism.Commands;
using Prism.Mvvm;

namespace Dlt645Master.App.ViewModels;

/// <summary>
/// 主窗口视图模型：把 <see cref="IMeterPollingService"/> 接到 WPF 绑定层。
/// 职责——维护连接/轮询状态机与五个命令、订阅服务三事件并经 <see cref="IUiDispatcher"/> 调度回界面线程、
/// 维护实时电参数集合（按 DI 去重就地更新）、报文监视集合（上限裁剪）与统计属性。
/// 地址/DI 一律显示序（高位在前），线序转换是 codec 内部的事，本层不碰。
/// </summary>
public sealed class MainWindowViewModel : BindableBase, IDisposable
{
    /// <summary>报文监视条目上限，超出后移除最旧。</summary>
    public const int MaxFrameLogEntries = 500;

    private static readonly TimeSpan ReadOnceTimeout = TimeSpan.FromMilliseconds(800);

    private readonly ITransport _transport;
    private readonly IMeterPollingService _service;
    private readonly IUiDispatcher _dispatcher;

    public MainWindowViewModel(ITransport transport, IMeterPollingService service, IUiDispatcher dispatcher)
    {
        _transport = transport;
        _service = service;
        _dispatcher = dispatcher;

        ConnectCommand = new DelegateCommand(ExecuteConnect, () => !IsConnected);
        DisconnectCommand = new DelegateCommand(ExecuteDisconnect, () => IsConnected);
        StartPollingCommand = new DelegateCommand(ExecuteStartPolling, () => IsConnected && !IsPolling && HasSelectedDataItem);
        StopPollingCommand = new DelegateCommand(ExecuteStopPolling, () => IsPolling);
        ReadOnceCommand = new DelegateCommand(ExecuteReadOnce, () => IsConnected && !IsPolling);

        foreach (DataItemDefinition definition in DataItemCatalog.All)
        {
            var option = new DataItemOption(definition, isSelected: true);
            option.PropertyChanged += OnDataItemOptionChanged;
            DataItems.Add(option);
        }

        _service.ReadCompleted += OnReadCompleted;
        _service.FrameTransferred += OnFrameTransferred;
        _service.StatisticsChanged += OnStatisticsChanged;
    }

    // ---- 绑定状态属性 ----

    private bool _isConnected;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private bool _isPolling;

    public bool IsPolling
    {
        get => _isPolling;
        private set
        {
            if (SetProperty(ref _isPolling, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private string _meterAddressText = AppDefaults.SimulatedMeterAddress;

    /// <summary>目标电表地址（12 位十六进制，显示序）。默认值与仿真从站地址一致。</summary>
    public string MeterAddressText
    {
        get => _meterAddressText;
        set => SetProperty(ref _meterAddressText, value);
    }

    private string _statusMessage = "就绪";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ---- 统计属性（消费 StatisticsChanged 快照，不自己记账）----

    private long _txCount;

    public long TxCount
    {
        get => _txCount;
        private set => SetProperty(ref _txCount, value);
    }

    private long _rxCount;

    public long RxCount
    {
        get => _rxCount;
        private set => SetProperty(ref _rxCount, value);
    }

    private long _timeoutCount;

    public long TimeoutCount
    {
        get => _timeoutCount;
        private set => SetProperty(ref _timeoutCount, value);
    }

    private long _errorCount;

    public long ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    private double _lastRoundTripMs;

    public double LastRoundTripMs
    {
        get => _lastRoundTripMs;
        private set => SetProperty(ref _lastRoundTripMs, value);
    }

    // ---- 绑定集合 ----

    /// <summary>可勾选的数据项清单，来自 <see cref="DataItemCatalog"/>，默认全选。</summary>
    public ObservableCollection<DataItemOption> DataItems { get; } = [];

    /// <summary>实时电参数卡片墙，按 DataId 去重就地更新。</summary>
    public ObservableCollection<MeterDataItemViewModel> Readings { get; } = [];

    /// <summary>报文监视条目，上限 <see cref="MaxFrameLogEntries"/> 条，满则移除最旧。</summary>
    public ObservableCollection<FrameLogEntry> FrameLog { get; } = [];

    // ---- 命令 ----

    public DelegateCommand ConnectCommand { get; }

    public DelegateCommand DisconnectCommand { get; }

    public DelegateCommand StartPollingCommand { get; }

    public DelegateCommand StopPollingCommand { get; }

    public DelegateCommand ReadOnceCommand { get; }

    private bool HasSelectedDataItem => DataItems.Any(option => option.IsSelected);

    // ---- 命令实现（CanExecute 是第一道防线，命令体内仍 try/catch 兜底，不让异常打穿界面）----

    private void ExecuteConnect()
    {
        try
        {
            _transport.Open();
            IsConnected = _transport.IsOpen;
            StatusMessage = IsConnected ? "已连接（仿真回环）" : "连接未成功";
        }
        catch (Exception ex)
        {
            IsConnected = _transport.IsOpen;
            StatusMessage = $"连接失败：{ex.Message}";
        }
    }

    private void ExecuteDisconnect()
    {
        try
        {
            if (_service.IsPolling)
            {
                _service.Stop(); // Stop 幂等
                IsPolling = _service.IsPolling;
            }

            _transport.Close();
            IsConnected = _transport.IsOpen;
            StatusMessage = "已断开";
        }
        catch (Exception ex)
        {
            StatusMessage = $"断开异常：{ex.Message}";
        }
    }

    private void ExecuteStartPolling()
    {
        if (!TryBuildAddress(out byte[] address))
        {
            return;
        }

        IReadOnlyList<byte[]> dataIds = SelectedDataIds();
        if (dataIds.Count == 0)
        {
            StatusMessage = "请至少选择一个数据项后再启动轮询。";
            return;
        }

        try
        {
            _service.Start(new PollingOptions
            {
                MeterAddress = address,
                DataIds = dataIds,
            });
            IsPolling = _service.IsPolling;
            StatusMessage = "轮询已启动。";
        }
        catch (Exception ex)
        {
            IsPolling = _service.IsPolling;
            StatusMessage = $"启动轮询失败：{ex.Message}";
        }
    }

    private void ExecuteStopPolling()
    {
        try
        {
            _service.Stop();
            IsPolling = _service.IsPolling;
            StatusMessage = "轮询已停止。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"停止轮询异常：{ex.Message}";
        }
    }

    private void ExecuteReadOnce()
    {
        if (!TryBuildAddress(out byte[] address))
        {
            return;
        }

        IReadOnlyList<byte[]> dataIds = SelectedDataIds();
        if (dataIds.Count == 0)
        {
            StatusMessage = "请至少选择一个数据项后再单次读取。";
            return;
        }

        try
        {
            MeterReadResult result = _service.ReadOnce(address, dataIds[0], ReadOnceTimeout);
            ApplyReadResult(result, DateTimeOffset.Now);
            StatusMessage = result.IsSuccess
                ? $"单次读取成功：{result.ItemName} = {result.Value} {result.Unit}"
                : $"单次读取失败：{result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"单次读取异常：{ex.Message}";
        }
    }

    private bool TryBuildAddress(out byte[] address)
    {
        if (MeterAddressParser.TryParse(MeterAddressText, out address))
        {
            return true;
        }

        StatusMessage = $"电表地址非法，需 12 位十六进制（显示序）：{MeterAddressText}";
        return false;
    }

    private IReadOnlyList<byte[]> SelectedDataIds()
        => DataItems.Where(option => option.IsSelected).Select(option => option.Definition.DataId).ToList();

    private void RaiseCommandStates()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        StartPollingCommand.RaiseCanExecuteChanged();
        StopPollingCommand.RaiseCanExecuteChanged();
        ReadOnceCommand.RaiseCanExecuteChanged();
    }

    private void OnDataItemOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataItemOption.IsSelected))
        {
            RaiseCommandStates();
        }
    }

    // ---- 服务事件（三处全部经 IUiDispatcher.Post 回界面线程）----

    private void OnReadCompleted(object? sender, MeterReadResultEventArgs e)
        => _dispatcher.Post(() => ApplyReadResult(e.Result, e.Timestamp));

    private void OnFrameTransferred(object? sender, FrameTransferredEventArgs e)
        => _dispatcher.Post(() =>
        {
            FrameLog.Add(new FrameLogEntry(e.Direction, e.Frame, e.Timestamp));
            while (FrameLog.Count > MaxFrameLogEntries)
            {
                FrameLog.RemoveAt(0);
            }
        });

    private void OnStatisticsChanged(object? sender, StatisticsChangedEventArgs e)
        => _dispatcher.Post(() =>
        {
            CommStatistics snapshot = e.Snapshot;
            TxCount = snapshot.TxFrameCount;
            RxCount = snapshot.RxFrameCount;
            TimeoutCount = snapshot.TimeoutCount;
            ErrorCount = snapshot.ErrorCount;
            LastRoundTripMs = snapshot.LastRoundTripMs;
        });

    /// <summary>
    /// 读取结果统一更新路径（轮询 ReadCompleted 与 ReadOnce 共用）。
    /// 带 DataId 的结果按 DataId 去重就地更新/新增卡片；无 DataId 的失败（超时/校验和错/异常应答无 DI）
    /// 无法对应到具体卡片，只反映到状态栏。
    /// </summary>
    private void ApplyReadResult(MeterReadResult result, DateTimeOffset timestamp)
    {
        if (result.DataId is not { } dataId)
        {
            if (!result.IsSuccess && result.ErrorMessage is { } message)
            {
                StatusMessage = $"读取失败：{message}";
            }

            return;
        }

        MeterDataItemViewModel? existing = Readings.FirstOrDefault(reading => reading.DataId.SequenceEqual(dataId));
        if (existing is null)
        {
            string itemName = result.ItemName ?? DataItemCatalog.Find(dataId)?.Name ?? HexFormat.Spaced(dataId);
            existing = new MeterDataItemViewModel(dataId, itemName);
            Readings.Add(existing);
        }

        existing.Update(result, timestamp);
    }

    public void Dispose()
    {
        _service.ReadCompleted -= OnReadCompleted;
        _service.FrameTransferred -= OnFrameTransferred;
        _service.StatisticsChanged -= OnStatisticsChanged;

        foreach (DataItemOption option in DataItems)
        {
            option.PropertyChanged -= OnDataItemOptionChanged;
        }

        _service.Dispose();
        _transport.Dispose();
    }
}
