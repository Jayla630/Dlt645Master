using Dlt645Master.App.Services;
using Dlt645Master.Core.Models;
using Prism.Mvvm;

namespace Dlt645Master.App.ViewModels;

/// <summary>数据项勾选条目：包裹一条 <see cref="DataItemDefinition"/>，附带是否参与轮询的 <see cref="IsSelected"/>。</summary>
public sealed class DataItemOption : BindableBase
{
    public DataItemOption(DataItemDefinition definition, bool isSelected = true)
    {
        Definition = definition;
        _isSelected = isSelected;
    }

    public DataItemDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Unit => Definition.Unit;

    /// <summary>数据标识（显示序）的空格分隔十六进制串，如 <c>00 01 00 00</c>。</summary>
    public string DataIdText => HexFormat.Spaced(Definition.DataId);

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
