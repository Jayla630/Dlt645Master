namespace Dlt645Master.Core.Protocol;

/// <summary>
/// Simple name -&gt; instance registry. Demo is single-protocol, so this deliberately does not use
/// Assembly.Load/reflection-based plugin discovery - adding a protocol means implementing
/// <see cref="IMeterProtocol"/> and adding one line below.
/// </summary>
public static class MeterProtocolRegistry
{
    public const string Dlt645ProtocolName = "DLT645-2007";

    private static readonly Dictionary<string, IMeterProtocol> Protocols =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Dlt645ProtocolName] = new Dlt645Protocol(),
        };

    public static IMeterProtocol Resolve(string protocolName)
    {
        if (Protocols.TryGetValue(protocolName, out IMeterProtocol? protocol))
        {
            return protocol;
        }

        throw new KeyNotFoundException($"未注册的协议：{protocolName}");
    }
}
