using Dlt645Master.App.Services;
using Dlt645Master.Core.Models;
using Prism.Mvvm;

namespace Dlt645Master.App.ViewModels;

/// <summary>
/// 电参数卡片条目。以数据标识（<see cref="DataId"/>，显示序）为主键，同一 DI 复用同一条目、就地刷新，
/// 而不是每次读取都新增一张卡片。<see cref="Value"/> / <see cref="Unit"/> / <see cref="UpdateTime"/> /
/// <see cref="IsSuccess"/> 均用 <c>SetProperty</c> 通知界面刷新。
/// </summary>
public sealed class MeterDataItemViewModel : BindableBase
{
    public MeterDataItemViewModel(byte[] dataId, string itemName)
    {
        DataId = dataId;
        DataIdText = HexFormat.Spaced(dataId);
        _itemName = itemName;
    }

    /// <summary>数据标识（4 字节，显示序），条目主键。</summary>
    public byte[] DataId { get; }

    public string DataIdText { get; }

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

    /// <summary>用一次读取结果就地刷新本条目。失败结果同样体现（<see cref="IsSuccess"/> 置 false）。</summary>
    public void Update(MeterReadResult result, DateTimeOffset timestamp)
    {
        if (result.ItemName is { } name)
        {
            ItemName = name;
        }

        Value = result.Value;
        Unit = result.Unit;
        IsSuccess = result.IsSuccess;
        UpdateTime = timestamp;
    }
}
