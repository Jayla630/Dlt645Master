namespace Dlt645Master.Core.Models;

/// <summary>
/// One row of the DL/T645-2007 Appendix A.2 data-identifier table.
/// </summary>
public sealed class DataItemDefinition
{
    /// <summary>Human-readable name (Chinese, matches the standard's terminology).</summary>
    public required string Name { get; init; }

    /// <summary>DI3 DI2 DI1 DI0, display/big-endian order, always 4 bytes.</summary>
    public required byte[] DataId { get; init; }

    /// <summary>Number of data bytes carrying the value in a response frame.</summary>
    public required int ByteLength { get; init; }

    /// <summary>Number of fractional decimal digits once the BCD digits are expanded.</summary>
    public required int DecimalPlaces { get; init; }

    /// <summary>Display format, e.g. "XXXXXX.XX" (documentation only; parsing uses ByteLength/DecimalPlaces).</summary>
    public required string Format { get; init; }

    public required string Unit { get; init; }
}
