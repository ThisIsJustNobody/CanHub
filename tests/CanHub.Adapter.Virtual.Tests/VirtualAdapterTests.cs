using CanHub.Adapter.Virtual.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Adapter.Virtual.Tests;

[TestClass]
public sealed class VirtualAdapterTests
{
    [TestInitialize]
    public void Setup()
    {
        // 每个测试前清理全局状态，避免测试间干扰
        VirtualBusStore.Clear();
    }

    [TestMethod(DisplayName = "打开虚拟适配器返回总线实例")]
    public async Task OpenAsync_ReturnsBus()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        Assert.IsNotNull(bus);
        Assert.IsTrue(bus.IsOpen);
        Assert.AreEqual("Virtual-bench1-CH0", bus.DisplayName);
        bus.Dispose();
    }

    [TestMethod(DisplayName = "发送帧后订阅者收到事件")]
    public async Task SendAsync_SubscriberReceives()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub = bus.Subscribe(new CanSubscriptionOptions());
        var frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02]);

        var result = await bus.SendAsync(frame, ct: TestContext.CancellationToken);
        Assert.IsTrue(result.Accepted);

        var evt = await sub.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, evt.ObservationKind);
        Assert.AreEqual(CanTransmitOutcome.Transmitted, evt.Outcome);
        Assert.AreEqual(frame, evt.Frame);
        Assert.AreEqual(result.CorrelationId, evt.CorrelationId);

        bus.Dispose();
    }

    [TestMethod(DisplayName = "禁用恢复时注入 bus-off 只上报状态")]
    public async Task RecoveryDisabled_InjectedBusOff_ReportsStatusOnly()
    {
        var provider = new VirtualAdapterProvider();
        var faultInjector = new VirtualFaultInjector();
        var endpoint = CanEndpoint.Parse("virtual://recovery-disabled?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions
        {
            NativeOptions = new VirtualRecoveryOptions { FaultInjector = faultInjector }
        });
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);
        var statuses = new List<CanStatusEvent>();
        bus.StatusChanged += statuses.Add;

        faultInjector.InjectBusOff();

        Assert.IsTrue(bus.IsOpen);
        Assert.HasCount(1, statuses);
        Assert.AreEqual(CanStatusCode.BusOff, statuses[0].Code);
        Assert.AreEqual(CanStatusSeverity.Critical, statuses[0].Severity);
        bus.Dispose();
    }

    [TestMethod(DisplayName = "CloseOnFault 在 bus-off 后关闭虚拟总线")]
    public async Task CloseOnFault_InjectedBusOff_ClosesBus()
    {
        var provider = new VirtualAdapterProvider();
        var faultInjector = new VirtualFaultInjector();
        var endpoint = CanEndpoint.Parse("virtual://recovery-close?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions
        {
            Recovery = CanRecoveryOptions.CloseOnFault(),
            NativeOptions = new VirtualRecoveryOptions { FaultInjector = faultInjector }
        });
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);
        var statuses = new List<CanStatusEvent>();
        bus.StatusChanged += statuses.Add;

        faultInjector.InjectBusOff();

        Assert.IsFalse(bus.IsOpen);
        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Disconnected },
            statuses.Select(static status => status.Code).ToArray());
        bus.Dispose();
    }

    [TestMethod(DisplayName = "ResetOnFault 在 bus-off 后执行一次重开并保持会话可发送")]
    public async Task ResetOnFault_InjectedBusOff_ReopensBusAndAllowsSend()
    {
        var provider = new VirtualAdapterProvider();
        var faultInjector = new VirtualFaultInjector();
        var endpoint = CanEndpoint.Parse("virtual://recovery-reset?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions
        {
            Recovery = CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
            NativeOptions = new VirtualRecoveryOptions { FaultInjector = faultInjector }
        });
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);
        var statuses = new List<CanStatusEvent>();
        bus.StatusChanged += statuses.Add;

        faultInjector.InjectBusOff();
        var result = await bus.SendAsync(
            CanFrame.CreateData(CanId.Standard(0x321), [0x01]),
            ct: TestContext.CancellationToken);

        Assert.IsTrue(bus.IsOpen);
        Assert.IsTrue(result.Accepted);
        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Recovered },
            statuses.Select(static status => status.Code).ToArray());
        bus.Dispose();
    }

    [TestMethod(DisplayName = "同总线不同通道共享消息")]
    public async Task DifferentChannels_SameBus_ShareMessages()
    {
        var provider = new VirtualAdapterProvider();
        var endpointCh0 = CanEndpoint.Parse("virtual://bench1?channel=0");
        var endpointCh1 = CanEndpoint.Parse("virtual://bench1?channel=1");
        var contextCh0 = new CanOpenContext(endpointCh0, new CanOpenOptions());
        var contextCh1 = new CanOpenContext(endpointCh1, new CanOpenOptions());

        var busCh0 = await provider.OpenAsync(contextCh0, TestContext.CancellationToken);
        var busCh1 = await provider.OpenAsync(contextCh1, TestContext.CancellationToken);

        // 通道 1 订阅
        using var subCh1 = busCh1.Subscribe(new CanSubscriptionOptions());

        // 通道 0 发送
        var frame = CanFrame.CreateData(CanId.Standard(0x100), [0x01]);
        var result = await busCh0.SendAsync(frame, ct: TestContext.CancellationToken);

        // 通道 1 应该收到 RX 事件
        var evtCh1 = await subCh1.ReadAsync(TestContext.CancellationToken);

        Assert.AreEqual(CanFrameDirection.Receive, evtCh1.Direction);
        Assert.AreEqual(CanFrameObservationKind.Bus, evtCh1.ObservationKind);
        Assert.AreEqual(1, evtCh1.ChannelIndex); // 通道 1 的索引
        Assert.AreEqual(0ul, evtCh1.CorrelationId); // RX 事件 CorrelationId = 0
        Assert.AreEqual(frame, evtCh1.Frame);

        busCh0.Dispose();
        busCh1.Dispose();
    }

    [TestMethod(DisplayName = "不同总线名称互相隔离")]
    public async Task DifferentBusNames_AreIsolated()
    {
        var provider = new VirtualAdapterProvider();
        var endpointBench1 = CanEndpoint.Parse("virtual://bench1?channel=0");
        var endpointBench2 = CanEndpoint.Parse("virtual://bench2?channel=0");
        var contextBench1 = new CanOpenContext(endpointBench1, new CanOpenOptions());
        var contextBench2 = new CanOpenContext(endpointBench2, new CanOpenOptions());

        var busBench1 = await provider.OpenAsync(contextBench1, TestContext.CancellationToken);
        var busBench2 = await provider.OpenAsync(contextBench2, TestContext.CancellationToken);

        // bench2 订阅
        using var subBench2 = busBench2.Subscribe(new CanSubscriptionOptions());

        // bench1 发送
        var frame = CanFrame.CreateData(CanId.Standard(0x200), [0x02]);
        await busBench1.SendAsync(frame, ct: TestContext.CancellationToken);

        // bench2 不应收到任何帧（用短超时令牌验证隔离性）
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => subBench2.ReadAsync(cts.Token).AsTask());

        busBench1.Dispose();
        busBench2.Dispose();
    }

    [TestMethod(DisplayName = "总线名称大小写不同的端点互相隔离")]
    public async Task BusNames_DifferentCasing_AreIsolated()
    {
        var provider = new VirtualAdapterProvider();
        var endpointUpper = CanEndpoint.Parse("virtual://Bench?channel=0");
        var endpointLower = CanEndpoint.Parse("virtual://bench?channel=0");
        var contextUpper = new CanOpenContext(endpointUpper, new CanOpenOptions());
        var contextLower = new CanOpenContext(endpointLower, new CanOpenOptions());

        var busUpper = await provider.OpenAsync(contextUpper, TestContext.CancellationToken);
        var busLower = await provider.OpenAsync(contextLower, TestContext.CancellationToken);

        using var subLower = busLower.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x201), [0x03]);
        await busUpper.SendAsync(frame, ct: TestContext.CancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => subLower.ReadAsync(cts.Token).AsTask());

        busUpper.Dispose();
        busLower.Dispose();
    }

    [TestMethod(DisplayName = "批量发送返回每帧结果")]
    public async Task SendBatchAsync_ReturnsPerFrameResults()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub = bus.Subscribe(new CanSubscriptionOptions());

        var frames = new[]
        {
            CanFrame.CreateData(CanId.Standard(0x301), [0x01]),
            CanFrame.CreateData(CanId.Standard(0x302), [0x02]),
            CanFrame.CreateData(CanId.Standard(0x303), [0x03]),
        };

        var results = await bus.SendBatchAsync(frames, ct: TestContext.CancellationToken);

        Assert.HasCount(3, results);
        foreach (var result in results)
        {
            Assert.IsTrue(result.Accepted);
            Assert.IsGreaterThan(0ul, result.CorrelationId);
        }

        // 验证 correlationId 唯一
        var ids = results.Select(r => r.CorrelationId).Distinct().ToArray();
        Assert.HasCount(3, ids, "每帧应有唯一 correlationId");

        bus.Dispose();
    }

    [TestMethod(DisplayName = "FD关闭时拒绝FD帧")]
    public async Task SendAsync_FdFalse_RejectsFdFrame()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0&fd=false");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        // FD 帧
        var fdFrame = CanFrame.CreateFdData(CanId.Standard(0x500), new byte[64]);
        var result = await bus.SendAsync(fdFrame, ct: TestContext.CancellationToken);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(CanTransmitSubmissionStatus.UnsupportedFeature, result.Status);
        Assert.IsGreaterThan(0ul, result.CorrelationId);

        // Classic CAN 帧应该正常
        var classicFrame = CanFrame.CreateData(CanId.Standard(0x501), [0x01]);
        var resultClassic = await bus.SendAsync(classicFrame, ct: TestContext.CancellationToken);
        Assert.IsTrue(resultClassic.Accepted);

        bus.Dispose();
    }

    [TestMethod(DisplayName = "FD关闭时批量拒绝FD帧仍返回唯一CorrelationId")]
    public async Task SendBatchAsync_FdFalse_RejectsFdFramesWithCorrelationIds()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0&fd=false");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        var frames = new[]
        {
            CanFrame.CreateFdData(CanId.Standard(0x510), new byte[64]),
            CanFrame.CreateFdData(CanId.Standard(0x511), new byte[64]),
        };

        var results = await bus.SendBatchAsync(frames, ct: TestContext.CancellationToken);

        Assert.HasCount(2, results);
        Assert.IsTrue(results.All(r => r.Status == CanTransmitSubmissionStatus.UnsupportedFeature));
        Assert.IsTrue(results.All(r => r.CorrelationId > 0));
        Assert.AreEqual(2, results.Select(r => r.CorrelationId).Distinct().Count());

        bus.Dispose();
    }

    [TestMethod(DisplayName = "未实现发送选项显式返回UnsupportedFeature")]
    public async Task SendAsync_UnsupportedOptions_ReturnsUnsupportedFeature()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);
        var frame = CanFrame.CreateData(CanId.Standard(0x512), [0x01]);
        var options = CanTransmitOptions.Create(mode: CanTransmitMode.SingleShot);

        var result = await bus.SendAsync(frame, options, TestContext.CancellationToken);

        Assert.AreEqual(CanTransmitSubmissionStatus.UnsupportedFeature, result.Status);
        Assert.IsGreaterThan(0ul, result.CorrelationId);

        bus.Dispose();
    }

    [TestMethod(DisplayName = "不可发送帧显式返回InvalidFrame")]
    public async Task SendAsync_NonTransmittableFrame_ReturnsInvalidFrame()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        var result = await bus.SendAsync(CanFrame.CreateError(0x10), ct: TestContext.CancellationToken);

        Assert.AreEqual(CanTransmitSubmissionStatus.InvalidFrame, result.Status);
        Assert.IsGreaterThan(0ul, result.CorrelationId);

        bus.Dispose();
    }

    [TestMethod(DisplayName = "多会话独立释放")]
    public async Task MultipleSessions_IndependentDispose()
    {
        var provider = new VirtualAdapterProvider();
        var endpointCh0 = CanEndpoint.Parse("virtual://bench1?channel=0");
        var endpointCh1 = CanEndpoint.Parse("virtual://bench1?channel=1");
        var contextCh0 = new CanOpenContext(endpointCh0, new CanOpenOptions());
        var contextCh1 = new CanOpenContext(endpointCh1, new CanOpenOptions());

        var busCh0 = await provider.OpenAsync(contextCh0, TestContext.CancellationToken);
        var busCh1 = await provider.OpenAsync(contextCh1, TestContext.CancellationToken);

        // 通道 1 订阅
        using var subCh1 = busCh1.Subscribe(new CanSubscriptionOptions());

        // 通道 0 发送，通道 1 应收到
        var frame = CanFrame.CreateData(CanId.Standard(0x600), [0x01]);
        await busCh0.SendAsync(frame, ct: TestContext.CancellationToken);

        var evtCh1 = await subCh1.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(CanFrameDirection.Receive, evtCh1.Direction);

        // 释放通道 0
        busCh0.Dispose();

        // 通道 1 仍然可用
        Assert.IsTrue(busCh1.IsOpen);

        // 再次从通道 1 发送并读取
        var frame2 = CanFrame.CreateData(CanId.Standard(0x601), [0x02]);
        await busCh1.SendAsync(frame2, ct: TestContext.CancellationToken);

        var evt2 = await subCh1.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(CanFrameDirection.Transmit, evt2.Direction);

        busCh1.Dispose();
    }

    [TestMethod(DisplayName = "跨通道序列号单调递增")]
    public async Task Sequence_MonotonicallyIncreasingAcrossChannels()
    {
        var provider = new VirtualAdapterProvider();
        var endpointCh0 = CanEndpoint.Parse("virtual://bench1?channel=0");
        var endpointCh1 = CanEndpoint.Parse("virtual://bench1?channel=1");
        var contextCh0 = new CanOpenContext(endpointCh0, new CanOpenOptions());
        var contextCh1 = new CanOpenContext(endpointCh1, new CanOpenOptions());

        var busCh0 = await provider.OpenAsync(contextCh0, TestContext.CancellationToken);
        var busCh1 = await provider.OpenAsync(contextCh1, TestContext.CancellationToken);

        using var subCh0 = busCh0.Subscribe(new CanSubscriptionOptions());
        using var subCh1 = busCh1.Subscribe(new CanSubscriptionOptions());

        // 通道 0 发送帧 1
        var frame1 = CanFrame.CreateData(CanId.Standard(0x701), [0x01]);
        await busCh0.SendAsync(frame1, ct: TestContext.CancellationToken);

        // 通道 0 和通道 1 各收到一个事件
        var evtCh0_1 = await subCh0.ReadAsync(TestContext.CancellationToken);
        var evtCh1_1 = await subCh1.ReadAsync(TestContext.CancellationToken);

        // 通道 0 发送帧 2
        var frame2 = CanFrame.CreateData(CanId.Standard(0x702), [0x02]);
        await busCh0.SendAsync(frame2, ct: TestContext.CancellationToken);

        var evtCh0_2 = await subCh0.ReadAsync(TestContext.CancellationToken);
        var evtCh1_2 = await subCh1.ReadAsync(TestContext.CancellationToken);

        // 序列号应单调递增
        Assert.IsLessThan(evtCh0_2.Sequence, evtCh0_1.Sequence,
            $"通道 0 序列号应递增: {evtCh0_1.Sequence} < {evtCh0_2.Sequence}");
        Assert.IsLessThan(evtCh1_2.Sequence, evtCh1_1.Sequence,
            $"通道 1 序列号应递增: {evtCh1_1.Sequence} < {evtCh1_2.Sequence}");

        busCh0.Dispose();
        busCh1.Dispose();
    }

    [TestMethod(DisplayName = "清单属性值正确")]
    public void Manifest_HasCorrectValues()
    {
        var provider = new VirtualAdapterProvider();

        Assert.AreEqual("virtual", provider.AdapterId);
        Assert.AreEqual("Virtual CAN Bus", provider.DisplayName);
        Assert.AreEqual("virtual", provider.Manifest.AdapterId);
        Assert.AreEqual("Virtual CAN Bus", provider.Manifest.DisplayName);
        CollectionAssert.Contains(provider.Manifest.EndpointSchemes.ToArray(), "virtual");
        CollectionAssert.Contains(provider.Manifest.Capabilities.Select(c => c.Name).ToArray(), "classic-can");
        CollectionAssert.Contains(provider.Manifest.Capabilities.Select(c => c.Name).ToArray(), "can-fd");
    }

    [TestMethod(DisplayName = "OpenAsync 在已取消令牌下抛出取消异常")]
    public async Task OpenAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => provider.OpenAsync(context, cts.Token).AsTask());
    }

    [TestMethod(DisplayName = "扩展方法可注册Virtual适配器")]
    public void RegistrationExtensions_RegisterVirtualAdapter()
    {
        var registry = CanHubRegistry.CreateDefault()
            .AddVirtualAdapter();

        Assert.IsNotNull(registry.FindAdapter("virtual"));

        var services = new ServiceCollection();
        services.AddCanHub()
            .AddVirtualAdapter();
        using var sp = services.BuildServiceProvider();

        var diRegistry = sp.GetRequiredService<CanHubRegistry>();
        Assert.IsNotNull(diRegistry.FindAdapter("virtual"));
    }

    #region 同端点重复打开

    [TestMethod(DisplayName = "同端点两次打开后双方均可收发")]
    public async Task SameEndpoint_OpenTwice_BothSessionsCanSendAndReceive()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var bus1 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var bus2 = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub1 = bus1.Subscribe(new CanSubscriptionOptions());
        using var sub2 = bus2.Subscribe(new CanSubscriptionOptions());

        // bus1 发送
        var frame1 = CanFrame.CreateData(CanId.Standard(0x100), [0x01]);
        var result1 = await bus1.SendAsync(frame1, ct: TestContext.CancellationToken);
        Assert.IsTrue(result1.Accepted);

        // 两个 session 都应收到事件（同一通道，共享 hub）
        var evt1 = await sub1.ReadAsync(TestContext.CancellationToken);
        var evt2 = await sub2.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(frame1, evt1.Frame);
        Assert.AreEqual(frame1, evt2.Frame);

        // bus2 发送
        var frame2 = CanFrame.CreateData(CanId.Standard(0x200), [0x02]);
        var result2 = await bus2.SendAsync(frame2, ct: TestContext.CancellationToken);
        Assert.IsTrue(result2.Accepted);

        var evt3 = await sub1.ReadAsync(TestContext.CancellationToken);
        var evt4 = await sub2.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(frame2, evt3.Frame);
        Assert.AreEqual(frame2, evt4.Frame);

        bus1.Dispose();
        bus2.Dispose();
    }

    [TestMethod(DisplayName = "同端点两次打开各自获得唯一CorrelationId")]
    public async Task SameEndpoint_OpenTwice_UniqueCorrelationIds()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var bus1 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var bus2 = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub1 = bus1.Subscribe(new CanSubscriptionOptions());
        using var sub2 = bus2.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x100), [0x01]);

        var result1 = await bus1.SendAsync(frame, ct: TestContext.CancellationToken);
        var result2 = await bus2.SendAsync(frame, ct: TestContext.CancellationToken);

        Assert.IsTrue(result1.Accepted);
        Assert.IsTrue(result2.Accepted);
        Assert.AreNotEqual(result1.CorrelationId, result2.CorrelationId,
            "两次发送的 CorrelationId 应唯一");

        bus1.Dispose();
        bus2.Dispose();
    }

    [TestMethod(DisplayName = "同端点多会话共享一次恢复序列")]
    public async Task SameEndpoint_OpenTwice_SharedRecoverySequence()
    {
        var provider = new VirtualAdapterProvider();
        var faultInjector = new VirtualFaultInjector();
        var endpoint = CanEndpoint.Parse("virtual://shared-recovery?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions
        {
            Recovery = CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
            NativeOptions = new VirtualRecoveryOptions { FaultInjector = faultInjector }
        });

        var bus1 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var bus2 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var statuses1 = new List<CanStatusEvent>();
        var statuses2 = new List<CanStatusEvent>();
        bus1.StatusChanged += statuses1.Add;
        bus2.StatusChanged += statuses2.Add;

        faultInjector.InjectBusOff();
        var send1 = await bus1.SendAsync(
            CanFrame.CreateData(CanId.Standard(0x701), [0x01]),
            ct: TestContext.CancellationToken);
        var send2 = await bus2.SendAsync(
            CanFrame.CreateData(CanId.Standard(0x702), [0x02]),
            ct: TestContext.CancellationToken);

        Assert.IsTrue(bus1.IsOpen);
        Assert.IsTrue(bus2.IsOpen);
        Assert.IsTrue(send1.Accepted);
        Assert.IsTrue(send2.Accepted);
        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Recovered },
            statuses1.Select(static status => status.Code).ToArray());
        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Recovered },
            statuses2.Select(static status => status.Code).ToArray());
        CollectionAssert.AreEqual(
            statuses1.Select(static status => status.Sequence).ToArray(),
            statuses2.Select(static status => status.Sequence).ToArray());

        bus1.Dispose();
        bus2.Dispose();
    }

    [TestMethod(DisplayName = "同端点释放一个session另一个仍可用")]
    public async Task SameEndpoint_DisposeOne_OtherStillWorks()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var bus1 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var bus2 = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub2 = bus2.Subscribe(new CanSubscriptionOptions());

        // 释放 bus1
        bus1.Dispose();
        Assert.IsFalse(bus1.IsOpen);

        // bus2 仍可发送
        var frame = CanFrame.CreateData(CanId.Standard(0x100), [0x01]);
        var result = await bus2.SendAsync(frame, ct: TestContext.CancellationToken);
        Assert.IsTrue(result.Accepted);

        var evt = await sub2.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(frame, evt.Frame);

        bus2.Dispose();
    }

    [TestMethod(DisplayName = "同端点全部释放后通道从全局存储中移除")]
    public async Task SameEndpoint_DisposeAll_ChannelRemovedFromStore()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var bus1 = await provider.OpenAsync(context, TestContext.CancellationToken);
        var bus2 = await provider.OpenAsync(context, TestContext.CancellationToken);

        bus1.Dispose();
        bus2.Dispose();

        // 重新打开同一端点，应获得全新实例
        var bus3 = await provider.OpenAsync(context, TestContext.CancellationToken);
        using var sub3 = bus3.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x300), [0x03]);
        var result = await bus3.SendAsync(frame, ct: TestContext.CancellationToken);
        Assert.IsTrue(result.Accepted);

        var evt = await sub3.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(frame, evt.Frame);

        bus3.Dispose();
    }

    [TestMethod(DisplayName = "同端点并发打开释放后重新打开可正常通信")]
    public async Task SameEndpoint_ConcurrentOpenDispose_ReopenWorks()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions());

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 100; i++)
                {
                    var bus = await provider.OpenAsync(context, TestContext.CancellationToken);
                    var subscription = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 4 });
                    await Task.Yield();
                    subscription.Dispose();
                    bus.Dispose();
                }
            }, TestContext.CancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);

        var reopened = await provider.OpenAsync(context, TestContext.CancellationToken);
        using var sub = reopened.Subscribe(new CanSubscriptionOptions());
        var frame = CanFrame.CreateData(CanId.Standard(0x555), [0x05]);

        var result = await reopened.SendAsync(frame, ct: TestContext.CancellationToken);
        Assert.IsTrue(result.Accepted);

        var evt = await sub.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(frame, evt.Frame);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);

        reopened.Dispose();
    }

    #endregion

    [TestMethod(DisplayName = "使用 CanBusParameters.Fd500k2M 允许FD帧")]
    public async Task SendAsync_FdDefault_AllowsFdFrame()
    {
        var provider = new VirtualAdapterProvider();
        var endpoint = CanEndpoint.Parse("virtual://bench1?channel=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M });
        var bus = await provider.OpenAsync(context, TestContext.CancellationToken);

        using var sub = bus.Subscribe(new CanSubscriptionOptions());

        var fdFrame = CanFrame.CreateFdData(CanId.Standard(0x700), new byte[64]);
        var result = await bus.SendAsync(fdFrame, ct: TestContext.CancellationToken);

        Assert.IsTrue(result.Accepted);

        var evt = await sub.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(fdFrame, evt.Frame);
        Assert.IsTrue(evt.Frame.Flags.HasFlag(CanFrameFlags.FD));

        bus.Dispose();
    }

    public TestContext TestContext { get; set; }
}
