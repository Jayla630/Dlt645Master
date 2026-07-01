using System.Globalization;

namespace Dlt645Master.Core.Protocol;

/// <summary>
/// Low-level, independently testable DL/T645-2007 byte-level transforms.
/// Pure functions only: no framing/validation/lookup logic lives here.
/// </summary>
public static class Dlt645FrameCodec
{
    private const int DataOffset = 0x33;

    /// <summary>
    /// CS = arithmetic sum of all bytes from the first 0x68 up to (excluding) CS itself, mod 256.
    /// Caller is responsible for slicing exactly that range.
    /// </summary>
    public static byte ComputeChecksum(ReadOnlySpan<byte> bytesFromFirst68ToBeforeChecksum)
    {
        int sum = 0;
        foreach (byte b in bytesFromFirst68ToBeforeChecksum)
        {
            sum += b;
        }

        return unchecked((byte)sum);
    }

    /// <summary>
    /// Wire encoding quirk: every DATA byte is transmitted as (raw + 0x33) so that control
    /// bytes like 0x68/0x16 never collide with data content on the wire.
    /// </summary>
    public static byte[] Add33H(ReadOnlySpan<byte> rawData)
    {
        byte[] result = new byte[rawData.Length];
        for (int i = 0; i < rawData.Length; i++)
        {
            result[i] = unchecked((byte)(rawData[i] + DataOffset));
        }

        return result;
    }

    /// <summary>Inverse of <see cref="Add33H"/>: subtract 0x33 from every received DATA byte.</summary>
    public static byte[] Sub33H(ReadOnlySpan<byte> transmittedData)
    {
        byte[] result = new byte[transmittedData.Length];
        for (int i = 0; i < transmittedData.Length; i++)
        {
            result[i] = unchecked((byte)(transmittedData[i] - DataOffset));
        }

        return result;
    }

    /// <summary>
    /// DL/T645 transmits multi-byte fields (address, DI, data value) low-byte-first.
    /// This flips between wire order and display/big-endian order (the operation is its own inverse).
    /// </summary>
    public static byte[] ReverseBytes(ReadOnlySpan<byte> bytes)
    {
        byte[] result = bytes.ToArray();
        Array.Reverse(result);
        return result;
    }

    /// <summary>
    /// Decodes BCD bytes (already in display/big-endian order, high nibble = more significant digit)
    /// into a decimal value, inserting the decimal point <paramref name="decimalPlaces"/> digits from the right.
    /// Example: bytes 00 00 01 86 with decimalPlaces=2 -> 1.86.
    /// </summary>
    public static decimal BcdToDecimal(ReadOnlySpan<byte> bytesDisplayOrder, int decimalPlaces)
    {
        Span<char> digits = stackalloc char[bytesDisplayOrder.Length * 2];
        for (int i = 0; i < bytesDisplayOrder.Length; i++)
        {
            byte b = bytesDisplayOrder[i];
            int high = b >> 4;
            int low = b & 0x0F;
            if (high > 9 || low > 9)
            {
                throw new FormatException($"Invalid BCD byte 0x{b:X2} at index {i}.");
            }

            digits[i * 2] = (char)('0' + high);
            digits[i * 2 + 1] = (char)('0' + low);
        }

        int decimalPointIndex = digits.Length - decimalPlaces;
        string intPart = decimalPointIndex > 0 ? new string(digits[..decimalPointIndex]) : "0";
        string fracPart = decimalPlaces > 0 ? new string(digits[Math.Max(decimalPointIndex, 0)..]) : string.Empty;
        string numeric = decimalPlaces > 0 ? $"{intPart}.{fracPart}" : intPart;

        return decimal.Parse(numeric, CultureInfo.InvariantCulture);
    }
}
