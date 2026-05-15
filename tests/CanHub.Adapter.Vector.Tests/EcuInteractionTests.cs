using CanHub;
using CanHub.Adapter.Vector;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class EcuInteractionTests
{
    [TestInitialize]
    public void CheckHardwareAvailable()
    {
        VectorEcuTestSettings.RequireOptIn();
    }

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// 扫描并打印所有通道的详细信息，确认全局索引。
    /// </summary>
    [TestMethod]
    public void Scan_DetailedChannelInfo()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var config = new XLClass.xl_driver_config();
            driver.XL_GetDriverConfig(ref config);

            TestContext.WriteLine($"Driver config: {config.channelCount} channels");
            for (int i = 0; i < config.channelCount && i < config.channel.Length; i++)
            {
                var ch = config.channel[i];
                TestContext.WriteLine($"  [{i}] hwType={ch.hwType}, name={ch.name?.TrimEnd('\0')}, channelIndex={ch.channelIndex}, mask=0x{(1UL << ch.channelIndex):X}");
            }
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 打开配置的 ECU 台架通道并接收信号。
    /// </summary>
    [TestMethod]
    public async Task Open_ConfiguredEcuChannel_ReceiveEcuSignals()
    {
        var target = VectorEcuTestSettings.GetTarget();
        var registry = CanHubRegistry.CreateDefault();
        registry.AddVectorAdapter();

        TestContext.WriteLine($"Opening ECU target: {target.Endpoint}");
        await using var bus = await registry.OpenAsync(
            target.Endpoint, new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M });

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 4096 });

        TestContext.WriteLine("Channel opened, waiting for ECU signals (2 seconds)...");

        // 等待接收 ECU 发送的信号
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var receivedCount = 0;
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var frame = await sub.ReadAsync(cts.Token);
                receivedCount++;
                if (receivedCount <= 5)
                {
                    var payload = new byte[frame.Frame.Length];
                    frame.Frame.CopyPayloadTo(payload);
                    TestContext.WriteLine($"  [{receivedCount}] ID=0x{frame.Frame.Id.Value:X}, Len={frame.Frame.Length}, Data={BitConverter.ToString(payload)}");
                }
            }
        }
        catch (OperationCanceledException) { }

        TestContext.WriteLine($"Total frames received: {receivedCount}");
        Assert.IsTrue(receivedCount > 0, "No frames received from ECU");
    }

    /// <summary>
    /// 发送 UDS 10 01 (DiagnosticSessionControl → DefaultSession)，期望收到 50 01。
    /// </summary>
    [TestMethod]
    public async Task Uds_DiagnosticSessionControl_DefaultSession()
    {
        var target = VectorEcuTestSettings.GetTarget();
        var registry = CanHubRegistry.CreateDefault();
        registry.AddVectorAdapter();

        await using var bus = await registry.OpenAsync(
            target.Endpoint, new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M });

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 4096 });

        // 清空缓冲区中已有的信号
        var clearCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { while (true) await sub.ReadAsync(clearCts.Token); }
        catch (OperationCanceledException) { }

        // 发送 UDS 10 01 — CAN FD 帧，DLC=8，无 BRS
        TestContext.WriteLine($"Sending UDS 10 01 on 0x{target.RequestId:X} (CAN FD frame)...");
        var request = CanFrame.CreateFdData(CanId.Standard(target.RequestId),
            [0x02, 0x10, 0x01, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC]);
        var result = await bus.SendAsync(request);
        TestContext.WriteLine($"Send result: {result.Status}, CorrelationId={result.CorrelationId}");

        // 等待响应 50 01
        TestContext.WriteLine($"Waiting for response on 0x{target.ResponseId:X}...");
        var responseCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var allIds = new HashSet<uint>();
        try
        {
            while (!responseCts.Token.IsCancellationRequested)
            {
                var response = await sub.ReadAsync(responseCts.Token);
                allIds.Add(response.Frame.Id.Value);

                if (response.Frame.Id.Value == target.ResponseId)
                {
                    var respPayload = new byte[response.Frame.Length];
                    response.Frame.CopyPayloadTo(respPayload);
                    TestContext.WriteLine($"  UDS Response: ID=0x{response.Frame.Id.Value:X}, Len={response.Frame.Length}, Data={BitConverter.ToString(respPayload)}");

                    // ISO 15765-2 Single Frame：首字节为 SF_DL，后续为 UDS 数据
                    Assert.IsTrue(respPayload.Length >= 3, "Response too short");
                    Assert.AreEqual(0x50, respPayload[1], "UDS response should be 0x50 (positive response)");
                    Assert.AreEqual(0x01, respPayload[2], "Session type should be 0x01 (Default Session)");
                    return;
                }
            }
            TestContext.WriteLine($"  No 0x{target.ResponseId:X} response. Received IDs: {string.Join(", ", allIds.Select(id => $"0x{id:X}"))}");
            Assert.Fail($"No UDS response on 0x{target.ResponseId:X}. Received {allIds.Count} frames on other IDs: {string.Join(", ", allIds.Select(id => $"0x{id:X}"))}");
        }
        catch (OperationCanceledException)
        {
            TestContext.WriteLine($"  Timeout. Received IDs: {string.Join(", ", allIds.Select(id => $"0x{id:X}"))}");
            Assert.Fail($"No UDS response on 0x{target.ResponseId:X} within 3 seconds. Received {allIds.Count} frames on other IDs.");
        }
    }
}
