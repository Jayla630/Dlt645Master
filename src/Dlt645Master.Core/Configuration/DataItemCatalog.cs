using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Configuration;

/// <summary>
/// 集中维护的、已支持的 DL/T645-2007 数据标识表。
/// TODO：以下数值是对附录 A.2（数据标识编码表）的初始尽力转录。
/// 对接真表前请逐行核对官方标准原文；本表与标准不一致时，以标准为准。
/// </summary>
public static class DataItemCatalog
{
    public static readonly DataItemDefinition ForwardActiveEnergy = new()
    {
        Name = "正向有功总电能",
        DataId = [0x00, 0x01, 0x00, 0x00],
        ByteLength = 4,
        DecimalPlaces = 2,
        Format = "XXXXXX.XX",
        Unit = "kWh",
        Category = DataItemCategory.Energy,
    };

    public static readonly DataItemDefinition ReverseActiveEnergy = new()
    {
        Name = "反向有功总电能",
        DataId = [0x00, 0x02, 0x00, 0x00],
        ByteLength = 4,
        DecimalPlaces = 2,
        Format = "XXXXXX.XX",
        Unit = "kWh",
        Category = DataItemCategory.Energy,
    };

    public static readonly DataItemDefinition VoltagePhaseA = new()
    {
        Name = "A 相电压",
        DataId = [0x02, 0x01, 0x01, 0x00],
        ByteLength = 2,
        DecimalPlaces = 1,
        Format = "XXX.X",
        Unit = "V",
        Category = DataItemCategory.Voltage,
    };

    public static readonly DataItemDefinition VoltagePhaseB = new()
    {
        Name = "B 相电压",
        DataId = [0x02, 0x01, 0x02, 0x00],
        ByteLength = 2,
        DecimalPlaces = 1,
        Format = "XXX.X",
        Unit = "V",
        Category = DataItemCategory.Voltage,
    };

    public static readonly DataItemDefinition VoltagePhaseC = new()
    {
        Name = "C 相电压",
        DataId = [0x02, 0x01, 0x03, 0x00],
        ByteLength = 2,
        DecimalPlaces = 1,
        Format = "XXX.X",
        Unit = "V",
        Category = DataItemCategory.Voltage,
    };

    public static readonly DataItemDefinition CurrentPhaseA = new()
    {
        Name = "A 相电流",
        DataId = [0x02, 0x02, 0x01, 0x00],
        ByteLength = 3,
        DecimalPlaces = 3,
        Format = "XXX.XXX",
        Unit = "A",
        Category = DataItemCategory.Current,
    };

    public static readonly DataItemDefinition CurrentPhaseB = new()
    {
        Name = "B 相电流",
        DataId = [0x02, 0x02, 0x02, 0x00],
        ByteLength = 3,
        DecimalPlaces = 3,
        Format = "XXX.XXX",
        Unit = "A",
        Category = DataItemCategory.Current,
    };

    public static readonly DataItemDefinition CurrentPhaseC = new()
    {
        Name = "C 相电流",
        DataId = [0x02, 0x02, 0x03, 0x00],
        ByteLength = 3,
        DecimalPlaces = 3,
        Format = "XXX.XXX",
        Unit = "A",
        Category = DataItemCategory.Current,
    };

    public static readonly DataItemDefinition TotalActivePower = new()
    {
        Name = "总有功功率",
        DataId = [0x02, 0x03, 0x00, 0x00],
        ByteLength = 3,
        DecimalPlaces = 4,
        Format = "XX.XXXX",
        Unit = "kW",
        Category = DataItemCategory.ActivePower,
    };

    public static readonly DataItemDefinition TotalPowerFactor = new()
    {
        Name = "总功率因数",
        DataId = [0x02, 0x06, 0x00, 0x00],
        ByteLength = 2,
        DecimalPlaces = 3,
        Format = "X.XXX",
        Unit = "",
        Category = DataItemCategory.PowerFactor,
    };

    // TODO: DI 待核对 —— 任务书标注电网频率数据标识尚未在标准原文中核实，先按占位值实现，
    // 上线前必须对照 DL/T645-2007 附录 A.2 校正 DI、字节数与小数位。
    public static readonly DataItemDefinition GridFrequency = new()
    {
        Name = "电网频率",
        DataId = [0x02, 0x80, 0x00, 0x07],
        ByteLength = 2,
        DecimalPlaces = 2,
        Format = "XX.XX",
        Unit = "Hz",
        Category = DataItemCategory.Frequency,
    };

    public static readonly IReadOnlyList<DataItemDefinition> All =
    [
        ForwardActiveEnergy,
        ReverseActiveEnergy,
        VoltagePhaseA,
        VoltagePhaseB,
        VoltagePhaseC,
        CurrentPhaseA,
        CurrentPhaseB,
        CurrentPhaseC,
        TotalActivePower,
        TotalPowerFactor,
        GridFrequency,
    ];

    /// <summary>按 DI3 DI2 DI1 DI0 字节（显示/高位在前序）查找对应定义。</summary>
    public static DataItemDefinition? Find(ReadOnlySpan<byte> dataId)
    {
        foreach (DataItemDefinition item in All)
        {
            if (dataId.SequenceEqual(item.DataId))
            {
                return item;
            }
        }

        return null;
    }
}
