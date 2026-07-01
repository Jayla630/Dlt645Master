using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Protocol;

/// <summary>
/// Pure, transport-agnostic meter protocol codec: byte[] in, byte[]/result out.
/// No serial port or UI dependency belongs behind this interface.
/// Adding a new protocol means implementing this interface and registering it in
/// <see cref="MeterProtocolRegistry"/> — no reflection-based plugin loading.
/// </summary>
public interface IMeterProtocol
{
    /// <summary>Builds a "read data" request frame for the given 6-byte address and 4-byte data identifier.</summary>
    byte[] BuildReadRequest(byte[] address, byte[] dataId);

    /// <summary>Attempts to parse a response frame. Never throws on malformed input; failures are reported via the result.</summary>
    MeterReadResult TryParseResponse(byte[] frame);
}
