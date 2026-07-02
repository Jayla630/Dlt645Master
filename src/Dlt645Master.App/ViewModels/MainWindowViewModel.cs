using System.Collections.ObjectModel;
using System.ComponentModel;
using Dlt645Master.App.Configuration;
using Dlt645Master.App.Services;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Services;
using Dlt645Master.Core.Transport;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Prism.Commands;
using Prism.Mvvm;
using SkiaSharp;

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

    /// <summary>电压趋势图滚动窗口点数：1 秒轮询 ≈ 最近 2 分钟。</summary>
    public const int MaxVoltagePoints = 120;

    /// <summary>电压上限警戒值（V），图表画红色水平参考线。</summary>
    public const double VoltageAlarmLimit = 250;

    private static readonly TimeSpan ReadOnceTimeout = TimeSpan.FromMilliseconds(800);

    // ---- 图表配色（SkiaSharp 侧无法引用 WPF 资源字典，此处按 DarkTheme.xaml 令牌值镜像；相色遵循
    // 电力行业 A 黄 / B 绿 / C 红 惯例，警戒线用状态红 + 虚线与 C 相实线区分）----
    private static readonly SKColor PhaseAColor = SKColor.Parse("#EAB308");
    private static readonly SKColor PhaseBColor = SKColor.Parse("#16A34A");
    private static readonly SKColor PhaseCColor = SKColor.Parse("#F87171");
    private static readonly SKColor AlarmColor = SKColor.Parse("#DC2626");
    private static readonly SKColor AxisTextColor = SKColor.Parse("#9CA3AF");
    private static readonly SKColor SeparatorColor = SKColor.Parse("#374151");

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

        VoltageSeries =
        [
            CreateVoltageLineSeries("A 相电压", _voltagePointsA, PhaseAColor),
            CreateVoltageLineSeries("B 相电压", _voltagePointsB, PhaseBColor),
            CreateVoltageLineSeries("C 相电压", _voltagePointsC, PhaseCColor),
        ];

        VoltageXAxes =
        [
            // 滚动窗口的横轴是「最近 N 次采样」，绝对刻度无意义：labeler 返回空串（不受主题默认画笔影响）、
            // 关闭分隔线，只留一条干净的时间推进轴。
            new Axis
            {
                Labeler = _ => string.Empty,
                ShowSeparatorLines = false,
                SeparatorsPaint = null,
                TicksPaint = null,
                SubticksPaint = null,
            },
        ];

        VoltageYAxes =
        [
            // 固定量程 180~260V，250V 红色警戒线才有稳定的视觉位置；自定义分隔线让 250 刻度有标签。
            new Axis
            {
                MinLimit = 180,
                MaxLimit = 260,
                CustomSeparators = [180, 200, 220, 240, VoltageAlarmLimit, 260],
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = new SolidColorPaint(SeparatorColor) { StrokeThickness = 1 },
                TextSize = 12,
            },
        ];

        VoltageSections =
        [
            new RectangularSection
            {
                Yi = VoltageAlarmLimit,
                Yj = VoltageAlarmLimit,
                Stroke = new SolidColorPaint(AlarmColor)
                {
                    StrokeThickness = 2,
                    PathEffect = new DashEffect([8f, 6f]),
                },
            },
        ];

        _service.ReadCompleted += OnReadCompleted;
        _service.FrameTransferred += OnFrameTransferred;
        _service.StatisticsChanged += OnStatisticsChanged;
    }

    /// <summary>三相电压趋势线的统一外观：细实线、无点标记、不平滑、关动画（工业上位机风格 + 降刷新开销）。</summary>
    private static LineSeries<double> CreateVoltageLineSeries(string name, ObservableCollection<double> values, SKColor color) => new()
    {
        Name = name,
        Values = values,
        Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
        Fill = null,
        GeometrySize = 0,
        GeometryFill = null,
        GeometryStroke = null,
        LineSmoothness = 0,
        AnimationsSpeed = TimeSpan.Zero,
    };

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

    // ---- 三相电压趋势图（LiveCharts2）----
    // 三条曲线各挂一个 ObservableCollection<double> 滚动缓冲；缓冲只在 IUiDispatcher 调度后的界面线程上增删
    // （LiveCharts2 在 WPF 下从后台线程改序列数据会随机渲染异常甚至崩溃）。

    private readonly ObservableCollection<double> _voltagePointsA = [];
    private readonly ObservableCollection<double> _voltagePointsB = [];
    private readonly ObservableCollection<double> _voltagePointsC = [];

    /// <summary>三条电压趋势线（A/B/C 相），绑定 CartesianChart.Series。</summary>
    public ObservableCollection<ISeries> VoltageSeries { get; }

    /// <summary>横轴：滚动采样序号，隐藏刻度。</summary>
    public Axis[] VoltageXAxes { get; }

    /// <summary>纵轴：固定量程 180~260V。</summary>
    public Axis[] VoltageYAxes { get; }

    /// <summary>250V 上限警戒线（红色虚线水平参考线），绑定 CartesianChart.Sections。</summary>
    public RectangularSection[] VoltageSections { get; }

    /// <summary>三相电压是否一项都没勾选——为 true 时图表区显示空态提示（不报错）。</summary>
    public bool IsVoltageChartUnsubscribed => !DataItems.Any(option =>
        option.IsSelected && FindVoltageBuffer(option.Definition.DataId) is not null);

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
            RaisePropertyChanged(nameof(IsVoltageChartUnsubscribed));
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

        if (result.IsSuccess && result.Value is { } value)
        {
            AppendVoltagePoint(dataId, value);
        }
    }

    /// <summary>
    /// 命中三相电压 DI（显示序比对）时把值追加到对应趋势缓冲，超 <see cref="MaxVoltagePoints"/> 裁掉最旧点。
    /// 本方法只会在 <see cref="ApplyReadResult"/>（已经 <see cref="IUiDispatcher"/> 调度到界面线程）内调用。
    /// </summary>
    private void AppendVoltagePoint(byte[] dataId, decimal value)
    {
        if (FindVoltageBuffer(dataId) is not { } buffer)
        {
            return;
        }

        buffer.Add((double)value);
        while (buffer.Count > MaxVoltagePoints)
        {
            buffer.RemoveAt(0);
        }
    }

    private ObservableCollection<double>? FindVoltageBuffer(byte[] dataId)
    {
        if (dataId.SequenceEqual(DataItemCatalog.VoltagePhaseA.DataId))
        {
            return _voltagePointsA;
        }

        if (dataId.SequenceEqual(DataItemCatalog.VoltagePhaseB.DataId))
        {
            return _voltagePointsB;
        }

        return dataId.SequenceEqual(DataItemCatalog.VoltagePhaseC.DataId) ? _voltagePointsC : null;
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
