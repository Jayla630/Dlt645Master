using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Services;
using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class MeterPollingServiceTests
{
    // 从站地址（显示序，高位在前），与 LoopbackTransportRoundTripTests 保持一致的写法。
    private static readonly byte[] SlaveDisplayAddress = Dlt645FrameCodec.ReverseBytes([0x01, 0x72, 0x00, 0x72, 0x00, 0x00]);

    private static (MeterPollingService Service, LoopbackTransport Transport) CreateOpenService(FixedMeterDataSource? dataSource = null)
    {
        dataSource ??= new FixedMeterDataSource();
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, dataSource);
        var transport = new LoopbackTransport(slave);
        transport.Open();
        var service = new MeterPollingService(transport, new Dlt645Protocol());
        return (service, transport);
    }

    [Fact]
    public void ReadOnce_KnownDataId_ReturnsSuccessfulResultAndUpdatesStatistics()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            var result = service.ReadOnce(SlaveDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId, TimeSpan.FromMilliseconds(200));

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(0.00m);

            CommStatistics stats = service.Statistics;
            stats.TxFrameCount.Should().Be(1);
            stats.RxFrameCount.Should().Be(1);
            stats.TimeoutCount.Should().Be(0);
            stats.ErrorCount.Should().Be(0);
        }

        transport.Dispose();
    }

    [Fact]
    public void ReadOnce_AddressMismatch_ReturnsFailureResultWithoutThrowingAndCountsTimeout()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            byte[] otherAddress = Dlt645FrameCodec.ReverseBytes([0x02, 0x72, 0x00, 0x72, 0x00, 0x00]);

            var result = service.ReadOnce(otherAddress, DataItemCatalog.ForwardActiveEnergy.DataId, TimeSpan.FromMilliseconds(50));

            result.IsSuccess.Should().BeFalse();
            service.Statistics.TimeoutCount.Should().Be(1);
            service.Statistics.ErrorCount.Should().Be(0);
        }

        transport.Dispose();
    }

    [Fact]
    public void ReadOnce_UnknownDataId_ReturnsAbnormalResponseResultAndCountsError()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            byte[] unknownDataId = [0xFF, 0xFF, 0xFF, 0xFF];

            var result = service.ReadOnce(SlaveDisplayAddress, unknownDataId, TimeSpan.FromMilliseconds(200));

            result.IsSuccess.Should().BeFalse();
            result.ControlCode.Should().Be(0xD1);

            CommStatistics stats = service.Statistics;
            stats.ErrorCount.Should().Be(1);
            stats.RxFrameCount.Should().Be(1);
        }

        transport.Dispose();
    }

    [Fact]
    public void ReadOnce_Success_RaisesFrameTransferredTwiceAndReadCompletedOnce()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            var frames = new List<FrameTransferredEventArgs>();
            int readCompletedCount = 0;
            service.FrameTransferred += (_, e) => frames.Add(e);
            service.ReadCompleted += (_, _) => Interlocked.Increment(ref readCompletedCount);

            byte[] expectedRequest = new Dlt645Protocol().BuildReadRequest(SlaveDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId);

            service.ReadOnce(SlaveDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId, TimeSpan.FromMilliseconds(200));

            frames.Should().HaveCount(2);
            frames[0].Direction.Should().Be(FrameDirection.Tx);
            frames[0].Frame.Should().Equal(expectedRequest);
            frames[1].Direction.Should().Be(FrameDirection.Rx);
            readCompletedCount.Should().Be(1);
        }

        transport.Dispose();
    }

    [Fact]
    public void Start_TransportNotOpen_ThrowsInvalidOperationException()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        using var transport = new LoopbackTransport(slave);
        using var service = new MeterPollingService(transport, new Dlt645Protocol());

        var options = new PollingOptions
        {
            MeterAddress = SlaveDisplayAddress,
            DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId],
        };

        Action act = () => service.Start(options);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Start_ThenPolls_RaisesAtLeastTwoReadCompletedEventsAndStopsCleanly()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            using var atLeastTwo = new CountdownEvent(2);
            service.ReadCompleted += (_, _) =>
            {
                if (!atLeastTwo.IsSet)
                {
                    atLeastTwo.Signal();
                }
            };

            var options = new PollingOptions
            {
                MeterAddress = SlaveDisplayAddress,
                DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId],
                PollInterval = TimeSpan.FromMilliseconds(10),
                ResponseTimeout = TimeSpan.FromMilliseconds(200),
                InterFrameDelay = TimeSpan.FromMilliseconds(5),
            };

            service.Start(options);

            atLeastTwo.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();

            service.Stop();
            service.IsPolling.Should().BeFalse();

            long countAfterStop = service.Statistics.RxFrameCount;
            Thread.Sleep(100);
            service.Statistics.RxFrameCount.Should().Be(countAfterStop);
        }

        transport.Dispose();
    }

    [Fact]
    public void Start_CalledTwice_ThrowsInvalidOperationException()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            var options = new PollingOptions
            {
                MeterAddress = SlaveDisplayAddress,
                DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId],
                PollInterval = TimeSpan.FromMilliseconds(10),
                InterFrameDelay = TimeSpan.FromMilliseconds(5),
            };

            service.Start(options);
            Action act = () => service.Start(options);

            act.Should().Throw<InvalidOperationException>();

            service.Stop();
        }

        transport.Dispose();
    }

    [Fact]
    public void Stop_CalledWithoutStart_DoesNotThrow()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            Action act = () => service.Stop();
            act.Should().NotThrow();
        }

        transport.Dispose();
    }

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            var options = new PollingOptions
            {
                MeterAddress = SlaveDisplayAddress,
                DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId],
                PollInterval = TimeSpan.FromMilliseconds(10),
                InterFrameDelay = TimeSpan.FromMilliseconds(5),
            };
            service.Start(options);
            Thread.Sleep(30);

            service.Stop();
            Action act = () => service.Stop();

            act.Should().NotThrow();
        }

        transport.Dispose();
    }

    [Fact]
    public void ReadOnce_WhilePolling_ThrowsInvalidOperationException()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            var options = new PollingOptions
            {
                MeterAddress = SlaveDisplayAddress,
                DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId],
                PollInterval = TimeSpan.FromMilliseconds(10),
                InterFrameDelay = TimeSpan.FromMilliseconds(5),
            };
            service.Start(options);

            Action act = () => service.ReadOnce(SlaveDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId, TimeSpan.FromMilliseconds(200));

            act.Should().Throw<InvalidOperationException>();

            service.Stop();
        }

        transport.Dispose();
    }

    [Fact]
    public void Start_WithTwoDataIdsOneUnknown_ProducesOneSuccessAndOneErrorPerRound()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            byte[] unknownDataId = [0xFF, 0xFF, 0xFF, 0xFF];
            var results = new List<MeterReadResult>();
            using var gotBoth = new CountdownEvent(2);
            service.ReadCompleted += (_, e) =>
            {
                results.Add(e.Result);
                if (!gotBoth.IsSet)
                {
                    gotBoth.Signal();
                }
            };

            var options = new PollingOptions
            {
                MeterAddress = SlaveDisplayAddress,
                DataIds = [DataItemCatalog.ForwardActiveEnergy.DataId, unknownDataId],
                PollInterval = TimeSpan.FromSeconds(10),
                ResponseTimeout = TimeSpan.FromMilliseconds(200),
                InterFrameDelay = TimeSpan.FromMilliseconds(5),
            };

            service.Start(options);

            gotBoth.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
            service.Stop();

            results.Should().HaveCount(2);
            results.Count(r => r.IsSuccess).Should().Be(1);
            results.Count(r => !r.IsSuccess).Should().Be(1);

            CommStatistics stats = service.Statistics;
            stats.RxFrameCount.Should().Be(2);
            stats.ErrorCount.Should().Be(1);
        }

        transport.Dispose();
    }

    [Fact]
    public void Statistics_ReadConcurrentlyDuringExchanges_StaysInternallyConsistent()
    {
        (MeterPollingService service, LoopbackTransport transport) = CreateOpenService();
        using (service)
        {
            for (int i = 0; i < 20; i++)
            {
                service.ReadOnce(SlaveDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId, TimeSpan.FromMilliseconds(200));
            }

            CommStatistics stats = service.Statistics;
            stats.TxFrameCount.Should().Be(20);
            stats.RxFrameCount.Should().Be(20);
            stats.ErrorCount.Should().Be(0);
            stats.TimeoutCount.Should().Be(0);
            stats.LastRoundTripMs.Should().BeGreaterThanOrEqualTo(0);
        }

        transport.Dispose();
    }
}
