namespace Dlt645Master.Core.Models;

/// <summary>
/// Result of parsing one DL/T645 frame. On checksum failure only <see cref="ErrorMessage"/> is
/// populated, since a bad checksum means no field in the frame can be trusted. On a recognized
/// abnormal response (control code 0xD1) or an unrecognized data identifier, the frame-level
/// fields (ControlCode/Address/DataId) are still trustworthy even though IsSuccess is false.
/// </summary>
public sealed class MeterReadResult
{
    public required bool IsSuccess { get; init; }

    public byte? ControlCode { get; init; }

    /// <summary>6-byte meter address, display/big-endian order.</summary>
    public byte[]? Address { get; init; }

    /// <summary>DI3 DI2 DI1 DI0, display/big-endian order.</summary>
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
