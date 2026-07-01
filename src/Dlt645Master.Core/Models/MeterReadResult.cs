namespace Dlt645Master.Core.Models;

/// <summary>
/// 解析一条 DL/T645 帧的结果。校验和失败时只会填充 <see cref="ErrorMessage"/>，
/// 因为校验和错误意味着帧内任何字段都不可信。若是已识别的异常应答（控制码 0xD1）
/// 或未识别的数据标识，即使 IsSuccess 为 false，帧级字段（ControlCode/Address/DataId）仍然可信。
/// </summary>
public sealed class MeterReadResult
{
    public required bool IsSuccess { get; init; }

    public byte? ControlCode { get; init; }

    /// <summary>6 字节电表地址，显示/高位在前序。</summary>
    public byte[]? Address { get; init; }

    /// <summary>DI3 DI2 DI1 DI0，显示/高位在前序。</summary>
    public byte[]? DataId { get; init; }

    public string? ItemName { get; init; }

    public decimal? Value { get; init; }

    public string? Unit { get; init; }

    public string? ErrorMessage { get; init; }

    public static MeterReadResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
    };
}
