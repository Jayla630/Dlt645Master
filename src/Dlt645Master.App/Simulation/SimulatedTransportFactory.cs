using Dlt645Master.App.Configuration;
using Dlt645Master.App.Services;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Transport;
using Dlt645Master.Transport.Simulation;

namespace Dlt645Master.App.Simulation;

/// <summary>
/// 仿真链路组装：固定值数据源 → 仿真从站 → 内存回环传输（<see cref="LoopbackTransport"/>）。
/// App 的 DI 装配与集成测试共用本工厂，确保「从站地址 + 预置数据」是单一来源。
/// 预置了 <see cref="DataItemCatalog"/> 全部数据项的合理演示值，使默认全选时每一项都能读到成功值。
/// </summary>
public static class SimulatedTransportFactory
{
    public static ITransport Create()
    {
        if (!MeterAddressParser.TryParse(AppDefaults.SimulatedMeterAddress, out byte[] address))
        {
            throw new InvalidOperationException($"内置仿真地址非法：{AppDefaults.SimulatedMeterAddress}");
        }

        var dataSource = new FixedMeterDataSource()
            .Set(DataItemCatalog.ForwardActiveEnergy.DataId, 12.34m)
            .Set(DataItemCatalog.ReverseActiveEnergy.DataId, 0.12m)
            .Set(DataItemCatalog.VoltagePhaseA.DataId, 220.1m)
            .Set(DataItemCatalog.VoltagePhaseB.DataId, 219.8m)
            .Set(DataItemCatalog.VoltagePhaseC.DataId, 220.5m)
            .Set(DataItemCatalog.CurrentPhaseA.DataId, 1.234m)
            .Set(DataItemCatalog.CurrentPhaseB.DataId, 1.210m)
            .Set(DataItemCatalog.CurrentPhaseC.DataId, 1.198m)
            .Set(DataItemCatalog.TotalActivePower.DataId, 0.8123m)
            .Set(DataItemCatalog.TotalPowerFactor.DataId, 0.998m)
            .Set(DataItemCatalog.GridFrequency.DataId, 50.00m);

        var slave = new Dlt645MeterSlave(address, dataSource);
        return new LoopbackTransport(slave);
    }
}
