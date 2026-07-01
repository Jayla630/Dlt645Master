namespace Dlt645Master.Core.Protocol;

/// <summary>
/// 简单的「名称 -&gt; 实例」注册表。demo 场景只有单一协议，因此刻意不使用
/// Assembly.Load / 基于反射的插件发现——新增协议只需实现 <see cref="IMeterProtocol"/> 并在下面加一行。
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
