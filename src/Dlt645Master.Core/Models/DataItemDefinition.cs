namespace Dlt645Master.Core.Models;

/// <summary>
/// DL/T645-2007 附录 A.2 数据标识表中的一行。
/// </summary>
public sealed class DataItemDefinition
{
    /// <summary>人类可读的名称（中文，与标准术语一致）。</summary>
    public required string Name { get; init; }

    /// <summary>DI3 DI2 DI1 DI0，显示/高位在前序，固定 4 字节。</summary>
    public required byte[] DataId { get; init; }

    /// <summary>应答帧中承载该数值的数据字节数。</summary>
    public required int ByteLength { get; init; }

    /// <summary>BCD 数字展开后的小数位数。</summary>
    public required int DecimalPlaces { get; init; }

    /// <summary>显示格式，如 "XXXXXX.XX"（仅作文档说明；实际解析用 ByteLength/DecimalPlaces）。</summary>
    public required string Format { get; init; }

    public required string Unit { get; init; }
}
