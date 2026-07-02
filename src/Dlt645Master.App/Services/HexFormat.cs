namespace Dlt645Master.App.Services;

/// <summary>报文/字节数组的十六进制显示辅助。</summary>
public static class HexFormat
{
    /// <summary>把字节数组格式化为空格分隔的大写十六进制串，如 <c>68 AA AA 68</c>；空数组返回空串。</summary>
    public static string Spaced(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        // 每字节 2 位十六进制 + 分隔空格，预留 3*len 的容量。
        return string.Create(bytes.Length * 3 - 1, bytes.ToArray(), static (span, source) =>
        {
            const string hexDigits = "0123456789ABCDEF";
            int pos = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (i > 0)
                {
                    span[pos++] = ' ';
                }

                span[pos++] = hexDigits[source[i] >> 4];
                span[pos++] = hexDigits[source[i] & 0x0F];
            }
        });
    }
}
