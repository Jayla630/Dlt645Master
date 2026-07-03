using Dlt645Master.App.Services;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Prism.Mvvm;

namespace Dlt645Master.App.ViewModels;

/// <summary>
/// 电参数卡片条目。以数据标识（<see cref="DataId"/>，显示序）为主键，同一 DI 复用同一条目、就地刷新，
/// 而不是每次读取都新增一张卡片。<see cref="Value"/> / <see cref="Unit"/> / <see cref="UpdateTime"/> /
/// <see cref="IsSuccess"/> / <see cref="Trend"/> 均用 <c>SetProperty</c> 通知界面刷新。
/// 分组/色带信息在构造时按 <see cref="DataItemCatalog"/> 的 <see cref="DataItemCategory"/> 一次算定（与波动标定表同源）。
/// </summary>
public sealed class MeterDataItemViewModel : BindableBase
{
    public MeterDataItemViewModel(byte[] dataId, string itemName)
    {
        DataId = dataId;
        DataIdText = HexFormat.Spaced(dataId);
        _itemName = itemName;

        DataItemDefinition? definition = DataItemCatalog.Find(dataId);
        Category = definition?.Category;
        (GroupName, GroupOrder) = ResolveGroup(Category);
        AccentToken = ResolveAccentToken(dataId, Category);
        IsVoltage = Category == DataItemCategory.Voltage;
        SortOrder = ResolveSortOrder(definition);
    }

    /// <summary>数据标识（4 字节，显示序），条目主键。</summary>
    public byte[] DataId { get; }

    public string DataIdText { get; }

    /// <summary>物理量类别；DI 未登记在目录时为 null。</summary>
    public DataItemCategory? Category { get; }

    /// <summary>卡片墙分组标题（电能 / 电压 / 电流 / 功率与频率），CollectionViewSource 按此分组。</summary>
    public string GroupName { get; }

    /// <summary>分组显示顺序（电能 0 → 电压 1 → 电流 2 → 功率与频率 3 → 其他 4）。</summary>
    public int GroupOrder { get; }

    /// <summary>组内排序键（目录索引，未登记 DI 排最后）。</summary>
    public int SortOrder { get; }

    /// <summary>
    /// 左侧分类色带令牌：电压三卡用曲线相色（PhaseA/PhaseB/PhaseC，与趋势图图例一致），
    /// 其余按组色（Energy/Current/Power），未登记 DI 为 Neutral。XAML 触发器按此映射主题画刷。
    /// </summary>
    public string AccentToken { get; }

    /// <summary>是否电压项——电压卡底部显示对 250V 上限的进度条。</summary>
    public bool IsVoltage { get; }

    private string _itemName;

    public string ItemName
    {
        get => _itemName;
        private set => SetProperty(ref _itemName, value);
    }

    private decimal? _value;

    public decimal? Value
    {
        get => _value;
        private set => SetProperty(ref _value, value);
    }

    private string? _unit;

    public string? Unit
    {
        get => _unit;
        private set => SetProperty(ref _unit, value);
    }

    private DateTimeOffset? _updateTime;

    public DateTimeOffset? UpdateTime
    {
        get => _updateTime;
        private set => SetProperty(ref _updateTime, value);
    }

    private bool _isSuccess;

    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    private ValueTrend _trend;

    /// <summary>相对上一次成功读数的涨跌；电能类恒 <see cref="ValueTrend.Flat"/>（不显示箭头）。</summary>
    public ValueTrend Trend
    {
        get => _trend;
        private set => SetProperty(ref _trend, value);
    }

    /// <summary>用一次读取结果就地刷新本条目。失败结果同样体现（<see cref="IsSuccess"/> 置 false）。</summary>
    public void Update(MeterReadResult result, DateTimeOffset timestamp)
    {
        if (result.ItemName is { } name)
        {
            ItemName = name;
        }

        Trend = ResolveTrend(result);
        Value = result.Value;
        Unit = result.Unit;
        IsSuccess = result.IsSuccess;
        UpdateTime = timestamp;
    }

    /// <summary>只有「上一次与本次都是成功数值」才可比较涨跌，其余情形（含电能类）一律持平。</summary>
    private ValueTrend ResolveTrend(MeterReadResult result)
    {
        if (Category == DataItemCategory.Energy
            || !result.IsSuccess
            || result.Value is not { } next
            || !IsSuccess
            || Value is not { } previous)
        {
            return ValueTrend.Flat;
        }

        return next > previous ? ValueTrend.Up
            : next < previous ? ValueTrend.Down
            : ValueTrend.Flat;
    }

    private static (string Name, int Order) ResolveGroup(DataItemCategory? category) => category switch
    {
        DataItemCategory.Energy => ("电能", 0),
        DataItemCategory.Voltage => ("电压", 1),
        DataItemCategory.Current => ("电流", 2),
        DataItemCategory.ActivePower or DataItemCategory.PowerFactor or DataItemCategory.Frequency => ("功率与频率", 3),
        _ => ("其他", 4),
    };

    private static string ResolveAccentToken(byte[] dataId, DataItemCategory? category)
    {
        if (category == DataItemCategory.Voltage)
        {
            return dataId.SequenceEqual(DataItemCatalog.VoltagePhaseA.DataId) ? "PhaseA"
                : dataId.SequenceEqual(DataItemCatalog.VoltagePhaseB.DataId) ? "PhaseB"
                : "PhaseC";
        }

        return category switch
        {
            DataItemCategory.Energy => "Energy",
            DataItemCategory.Current => "Current",
            DataItemCategory.ActivePower or DataItemCategory.PowerFactor or DataItemCategory.Frequency => "Power",
            _ => "Neutral",
        };
    }

    private static int ResolveSortOrder(DataItemDefinition? definition)
    {
        for (int i = 0; i < DataItemCatalog.All.Count; i++)
        {
            if (ReferenceEquals(DataItemCatalog.All[i], definition))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
