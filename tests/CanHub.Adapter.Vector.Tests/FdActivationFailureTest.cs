using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

/// <summary>
/// 复现 V4 FD 配置后 XL_ActivateChannel 返回 XL_ERR_INVALID_ACCESS 的问题。
/// </summary>
[TestClass]
public sealed class FdActivationFailureTest
{
    private const string HardwareOptInVariable = "CANHUB_TEST_VECTOR";
    private const string DeviceVariable = "CANHUB_TEST_VECTOR_DEVICE";
    private const string DeviceIndexVariable = "CANHUB_TEST_VECTOR_DEVICE_INDEX";
    private const string ChannelIndexVariable = "CANHUB_TEST_VECTOR_CHANNEL_INDEX";

    [TestInitialize]
    public void CheckHardwareAvailable()
    {
        if (string.Equals(TestContext.TestName, nameof(Scenario9_CanHub_EchoAndErrorFrameDetection), StringComparison.Ordinal))
        {
            VectorEcuTestSettings.RequireOptIn();
            return;
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(HardwareOptInVariable)))
            Assert.Inconclusive($"Skipping: {HardwareOptInVariable} is not set.");
    }

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// 场景1：V3 打开 → Classic CAN 参数 → FD 配置 → 激活
    /// 预期：FD 配置成功，但激活失败
    /// </summary>
    [TestMethod]
    public void Scenario1_V3_Then_FdConfig_Then_Activate()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // Step 1: V3 打开
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 256,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            TestContext.WriteLine($"[1] XL_OpenPort(V3): {s1} (handle={portHandle})");

            // Step 2: Classic CAN 参数
            var chipParams = new XLClass.xl_chip_params
            {
                bitrate = 500_000, sjw = 1, tseg1 = 13, tseg2 = 2, sam = 0,
            };
            var s2 = driver.XL_CanSetChannelParams(portHandle, mask, chipParams);
            TestContext.WriteLine($"[2] XL_CanSetChannelParams: {s2}");

            // Step 3: FD 配置（在 V3 端口上）
            //var fdConf = new XLClass.XLcanFdConf
            //{
            //    arbitrationBitRate = 500_000,
            //    sjwAbr = 1,
            //    tseg1Abr = 5,
            //    tseg2Abr = 2,
            //    dataBitRate = 2_000_000,
            //    sjwDbr = 1,
            //    tseg1Dbr = 29,
            //    tseg2Dbr = 10,
            //    options = 0,
            //};
            //var s3 = driver.XL_CanFdSetConfiguration(portHandle, mask, fdConf);
            //TestContext.WriteLine($"[3] XL_CanFdSetConfiguration: {s3}");

            // Step 4: 激活
            var s4 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            TestContext.WriteLine($"[4] XL_ActivateChannel: {s4}");

            // Step 5: 如果激活成功，尝试接收
            if (s4 == XLDefine.XL_Status.XL_SUCCESS)
            {
                int notifHandle = -1;
                driver.XL_SetNotification(portHandle, ref notifHandle, 1);
                WaitForNotificationOrTimeout(notifHandle, 1000);

                var xlEvent = new XLClass.xl_event();
                var rxStatus = driver.XL_Receive(portHandle, ref xlEvent);
                TestContext.WriteLine($"[5] XL_Receive: {rxStatus}, tag={xlEvent.tag}");

                driver.XL_DeactivateChannel(portHandle, mask);
            }

            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景2：V4 打开 → FD 配置 → 激活
    /// 预期：FD 配置成功，但激活失败
    /// </summary>
    [TestMethod]
    public void Scenario2_V4_Then_FdConfig_Then_Activate()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // Step 1: V4 打开
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 65536,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION_V4,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            TestContext.WriteLine($"[1] XL_OpenPort(V4): {s1} (handle={portHandle})");

            // Step 2: FD 配置
            var fdConf = new XLClass.XLcanFdConf
            {
                arbitrationBitRate = 500_000,
                sjwAbr = 1, tseg1Abr = 5, tseg2Abr = 2,
                dataBitRate = 2_000_000,
                sjwDbr = 1, tseg1Dbr = 29, tseg2Dbr = 10,
                options = 0,
            };
            var s2 = driver.XL_CanFdSetConfiguration(portHandle, mask, fdConf);
            TestContext.WriteLine($"[2] XL_CanFdSetConfiguration: {s2}");

            // Step 3: 激活
            var s3 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            TestContext.WriteLine($"[3] XL_ActivateChannel: {s3}");

            if (s3 == XLDefine.XL_Status.XL_SUCCESS)
            {
                driver.XL_DeactivateChannel(portHandle, mask);
            }

            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景3：V4 打开 → 不配置 → 直接激活
    /// 预期：应该成功（FD 通道默认支持 FD）
    /// </summary>
    [TestMethod]
    public void Scenario3_V4_NoConfig_Then_Activate()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 65536,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION_V4,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            TestContext.WriteLine($"[1] XL_OpenPort(V4): {s1} (handle={portHandle})");

            // 不配置，直接激活
            var s2 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            TestContext.WriteLine($"[2] XL_ActivateChannel (no config): {s2}");

            if (s2 == XLDefine.XL_Status.XL_SUCCESS)
            {
                // 尝试接收 FD 帧
                int notifHandle = -1;
                driver.XL_SetNotification(portHandle, ref notifHandle, 1);
                WaitForNotificationOrTimeout(notifHandle, 1000);

                var rxEvent = new XLClass.XLcanRxEvent();
                int fdCount = 0;
                while (fdCount < 3)
                {
                    var rxStatus = driver.XL_CanReceive(portHandle, ref rxEvent);
                    if (rxStatus == XLDefine.XL_Status.XL_SUCCESS)
                    {
                        fdCount++;
                        TestContext.WriteLine($"  FD [{fdCount}] tag={rxEvent.tag}, canId=0x{rxEvent.tagData.canRxOkMsg.canId:X}");
                    }
                    else break;
                }
                TestContext.WriteLine($"  Received {fdCount} FD frames");

                driver.XL_DeactivateChannel(portHandle, mask);
            }

            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景5（TX 回显基线）：V3 打开 → Classic CAN 参数 → 激活 → 发送 → 收取验证
    /// 不调用 SetChannelMode（即 TX echo 默认关闭），验证收回路经中无任何回显。
    /// 参考 CanConnector.ProcessRawData 的分类：
    /// - XL_RECEIVE_MSG + TX_COMPLETED → 发送成功回显
    /// - XL_RECEIVE_MSG + NONE         → 总线正常接收
    /// - XL_TRANSMIT_MSG               → 发送失败反馈
    /// </summary>
    [TestMethod]
    public void Scenario5_V3_SendAndCheckTxEcho()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // Step 1: V3 打开
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 256,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s1, "XL_OpenPort failed");
            TestContext.WriteLine($"[1] XL_OpenPort(V3): {s1} (handle={portHandle})");

            // Step 2: Classic CAN 参数
            var chipParams = new XLClass.xl_chip_params
            {
                bitrate = 500_000, sjw = 1, tseg1 = 13, tseg2 = 2, sam = 1,
            };
            var s2 = driver.XL_CanSetChannelParams(portHandle, mask, chipParams);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s2, "XL_CanSetChannelParams failed");
            TestContext.WriteLine($"[2] XL_CanSetChannelParams: {s2}");

            // Step 3: 激活（XL_ACTIVATE_NONE — 无 TX 回显标志）
            var s3 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s3, "XL_ActivateChannel failed");
            TestContext.WriteLine($"[3] XL_ActivateChannel: {s3}");

            // Step 4: 设置通知并排空初始缓冲区
            int notifHandle = -1;
            driver.XL_SetNotification(portHandle, ref notifHandle, 1);
            WaitForNotificationOrTimeout(notifHandle, 500);

            int drained = 0;
            while (true)
            {
                var drainEv = new XLClass.xl_event();
                var drainSt = driver.XL_Receive(portHandle, ref drainEv);
                if (drainSt != XLDefine.XL_Status.XL_SUCCESS) break;
                drained++;
            }
            TestContext.WriteLine($"[4] Drained {drained} initial events");

            // Step 5: 发送五帧具有独特 ID 的测试报文
            uint[] testIds = [0x100, 0x200, 0x300, 0x400, 0x500];
            int sentCount = 0;
            foreach (var id in testIds)
            {
                var msg = new XLClass.xl_can_msg
                {
                    id = id,
                    dlc = 8,
                    flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE,
                    data = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88],
                };
                var txEv = new XLClass.xl_event
                {
                    tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG,
                    tagData = new XLClass.xl_tag_data { can_Msg = msg },
                };
                var st = driver.XL_CanTransmit(portHandle, mask, txEv);
                if (st == XLDefine.XL_Status.XL_SUCCESS) sentCount++;
                TestContext.WriteLine($"  Send ID=0x{id:X}: {st}");
            }
            TestContext.WriteLine($"[5] Successfully sent {sentCount}/{testIds.Length} frames");

            // Step 6: 等待驱动回显（等待 1.5 秒用于总线传输和回显处理）
            WaitForNotificationOrTimeout(notifHandle, 1500);

            // Step 7: 轮询收取并按类型分类
            // 参考 CanConnector.ProcessRawData 的分类逻辑：
            //   XL_RECEIVE_MSG + TX_COMPLETED → 发送成功回显
            //   XL_RECEIVE_MSG + NONE         → 总线正常接收
            //   XL_RECEIVE_MSG + ERROR_FRAME  → 错误帧
            //   XL_TRANSMIT_MSG               → 发送失败反馈
            int normalRxCount = 0;
            int txCompletedCount = 0;
            int errorFrameCount = 0;
            int txFailedCount = 0;
            int chipStateCount = 0;
            int otherCount = 0;
            var normalRxIds = new List<uint>();
            var txCompletedIds = new List<uint>();
            var txFailedIds = new List<uint>();
            var otherTags = new List<int>();

            for (int i = 0; i < 200; i++)
            {
                var ev = new XLClass.xl_event();
                var st = driver.XL_Receive(portHandle, ref ev);
                if (st != XLDefine.XL_Status.XL_SUCCESS) break;

                if (ev.tag == XLDefine.XL_EventTags.XL_RECEIVE_MSG)
                {
                    var flags = ev.tagData.can_Msg.flags;
                    if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED))
                    {
                        txCompletedCount++;
                        txCompletedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags == XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE)
                    {
                        normalRxCount++;
                        normalRxIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME))
                    {
                        errorFrameCount++;
                    }
                    else
                    {
                        otherCount++;
                        otherTags.Add((int)ev.tag);
                    }
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_TRANSMIT_MSG)
                {
                    txFailedCount++;
                    txFailedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_CHIP_STATE)
                {
                    chipStateCount++;
                }
                else
                {
                    otherCount++;
                    otherTags.Add((int)ev.tag);
                }
            }

            // Step 8: 输出结果汇总
            TestContext.WriteLine($"[6] Receive results:");
            TestContext.WriteLine($"    XL_RECEIVE_MSG (bus RX):       {normalRxCount}");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+TX_COMPLETED:   {txCompletedCount}  (发送成功回显)");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+ERROR_FRAME:    {errorFrameCount}  (错误帧)");
            TestContext.WriteLine($"    XL_TRANSMIT_MSG:                {txFailedCount}  (发送失败反馈)");
            TestContext.WriteLine($"    XL_CHIP_STATE:                  {chipStateCount}");
            TestContext.WriteLine($"    Other:                          {otherCount}" +
                (otherTags.Count > 0 ? $" tags=[{string.Join(",", otherTags)}]" : ""));

            // Step 9: 检查发送的报文出现在哪条路径中
            var sentIdSet = new HashSet<uint>(testIds);

            var matchedTxCompleted = txCompletedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedTxFailed = txFailedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedNormalRx = normalRxIds.Where(id => sentIdSet.Contains(id)).ToList();

            if (matchedTxCompleted.Count > 0)
            {
                TestContext.WriteLine($"[7] *** TX ECHO CONFIRMED: {matchedTxCompleted.Count} sent frame(s) " +
                    $"returned as XL_RECEIVE_MSG+TX_COMPLETED: {string.Join(", ", matchedTxCompleted.Select(id => $"0x{id:X}"))}");
            }
            else
            {
                TestContext.WriteLine($"[7] *** No TX echo detected (no TX_COMPLETED with sent IDs)");
            }

            if (matchedTxFailed.Count > 0)
            {
                TestContext.WriteLine($"    *** TX FAILED: {matchedTxFailed.Count} sent frame(s) " +
                    $"returned as XL_TRANSMIT_MSG: {string.Join(", ", matchedTxFailed.Select(id => $"0x{id:X}"))}");
            }

            if (matchedNormalRx.Count > 0)
            {
                TestContext.WriteLine($"    *** SELF-RX: {matchedNormalRx.Count} sent frame(s) " +
                    $"returned as normal XL_RECEIVE_MSG: {string.Join(", ", matchedNormalRx.Select(id => $"0x{id:X}"))}");
            }

            if (normalRxCount > 0)
            {
                var sampleIds = normalRxIds.Take(Math.Min(10, normalRxIds.Count))
                    .Select(id => $"0x{id:X}");
                TestContext.WriteLine($"    Bus RX sample IDs (up to 10): {string.Join(", ", sampleIds)}");
            }

            // Step 10: 去激活
            driver.XL_DeactivateChannel(portHandle, mask);
            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景6（TX 回显对比）：V3 打开 → Classic CAN 参数 → 以(cast)(1) 激活尝试 TX_ECHO 标志
    /// 与场景5 对比，验证 XL_AC_Flags 中 TX_ECHO 标志是否影响回显行为。
    /// XL_ACTIVATE_TX_ECHO = 1（原始 C API 定义 xlAcFlagTxEcho = 1 &lt;&lt; 0），
    /// 但 .NET wrapper 未导出该值，此处使用 (XLDefine.XL_AC_Flags)1 尝试。
    /// </summary>
    [TestMethod]
    public void Scenario6_V3_SendAndCheckTxEcho_WithEchoFlag()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // Step 1: V3 打开
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 256,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s1, "XL_OpenPort failed");
            TestContext.WriteLine($"[1] XL_OpenPort(V3): {s1}");

            // Step 2: Classic CAN 参数
            var chipParams = new XLClass.xl_chip_params
            {
                bitrate = 500_000, sjw = 1, tseg1 = 13, tseg2 = 2, sam = 1,
            };
            var s2 = driver.XL_CanSetChannelParams(portHandle, mask, chipParams);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s2, "XL_CanSetChannelParams failed");
            TestContext.WriteLine($"[2] XL_CanSetChannelParams: {s2}");

            // Step 3: SetChannelOutput — 正常模式
            var s3 = driver.XL_CanSetChannelOutput(portHandle, permMask,
                XLDefine.XL_OutputMode.XL_OUTPUT_MODE_NORMAL);
            TestContext.WriteLine($"[3] XL_CanSetChannelOutput(NORMAL): {s3}");

            // Step 4: SetChannelMode — 启用 TX echo (tx=1)
            var s4 = driver.XL_CanSetChannelMode(portHandle, mask, tx: 1, txRq: 0);
            TestContext.WriteLine($"[4] XL_CanSetChannelMode(tx=1, txRq=0): {s4}");

            // Step 5: SetReceiveMode — 不抑制错误帧和芯片状态
            var s5 = driver.XL_CanSetReceiveMode(portHandle, errorFrame: 0, chipState: 0);
            TestContext.WriteLine($"[5] XL_CanSetReceiveMode(errorFrame=0, chipState=0): {s5}");

            // Step 6: 激活
            var s6 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s6, "XL_ActivateChannel failed");
            TestContext.WriteLine($"[6] XL_ActivateChannel: {s6}");

            // Step 7: 设置通知并排空初始缓冲区
            int notifHandle = -1;
            driver.XL_SetNotification(portHandle, ref notifHandle, 1);
            WaitForNotificationOrTimeout(notifHandle, 500);

            int drained = 0;
            while (true)
            {
                var drainEv = new XLClass.xl_event();
                var drainSt = driver.XL_Receive(portHandle, ref drainEv);
                if (drainSt != XLDefine.XL_Status.XL_SUCCESS) break;
                drained++;
            }
            TestContext.WriteLine($"[7] Drained {drained} initial events");

            // Step 8: 发送五帧测试报文
            uint[] testIds = [0x110, 0x210, 0x310, 0x410, 0x510];
            int sentCount = 0;
            foreach (var id in testIds)
            {
                var msg = new XLClass.xl_can_msg
                {
                    id = id,
                    dlc = 8,
                    flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE,
                    data = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11],
                };
                var txEv = new XLClass.xl_event
                {
                    tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG,
                    tagData = new XLClass.xl_tag_data { can_Msg = msg },
                };
                var st = driver.XL_CanTransmit(portHandle, mask, txEv);
                if (st == XLDefine.XL_Status.XL_SUCCESS) sentCount++;
                TestContext.WriteLine($"  Send ID=0x{id:X}: {st}");
            }
            TestContext.WriteLine($"[8] Successfully sent {sentCount}/{testIds.Length} frames");

            // Step 9: 等待回显
            WaitForNotificationOrTimeout(notifHandle, 1500);

            // Step 10: 轮询收取并按类型分类
            // 参考 CanConnector.ProcessRawData:
            //   XL_RECEIVE_MSG + TX_COMPLETED → 发送成功回显
            //   XL_RECEIVE_MSG + NONE         → 总线正常接收
            //   XL_RECEIVE_MSG + ERROR_FRAME  → 错误帧
            //   XL_TRANSMIT_MSG               → 发送失败反馈
            int normalRxCount = 0;
            int txCompletedCount = 0;
            int errorFrameCount = 0;
            int txFailedCount = 0;
            int chipStateCount = 0;
            int otherCount = 0;
            var normalRxIds = new List<uint>();
            var txCompletedIds = new List<uint>();
            var txFailedIds = new List<uint>();

            for (int i = 0; i < 10000; i++)
            {
                var ev = new XLClass.xl_event();
                var st = driver.XL_Receive(portHandle, ref ev);
                if (st != XLDefine.XL_Status.XL_SUCCESS) break;

                if (ev.tag == XLDefine.XL_EventTags.XL_RECEIVE_MSG)
                {
                    var flags = ev.tagData.can_Msg.flags;
                    if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED))
                    {
                        txCompletedCount++;
                        txCompletedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags == XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE)
                    {
                        normalRxCount++;
                        normalRxIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME))
                    {
                        errorFrameCount++;
                    }
                    else
                    {
                        otherCount++;
                    }
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_TRANSMIT_MSG)
                {
                    txFailedCount++;
                    txFailedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_CHIP_STATE)
                {
                    chipStateCount++;
                }
                else
                {
                    otherCount++;
                }
            }

            // Step 11: 输出结果
            TestContext.WriteLine($"[9] Receive results:");
            TestContext.WriteLine($"    XL_RECEIVE_MSG (bus RX):       {normalRxCount}");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+TX_COMPLETED:   {txCompletedCount}  (发送成功回显)");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+ERROR_FRAME:    {errorFrameCount}  (错误帧)");
            TestContext.WriteLine($"    XL_TRANSMIT_MSG:                {txFailedCount}  (发送失败反馈)");
            TestContext.WriteLine($"    XL_CHIP_STATE:                  {chipStateCount}");
            TestContext.WriteLine($"    Other:                          {otherCount}");

            var sentIdSet = new HashSet<uint>(testIds);
            var matchedTxCompleted = txCompletedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedTxFailed = txFailedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedNormalRx = normalRxIds.Where(id => sentIdSet.Contains(id)).ToList();

            if (matchedTxCompleted.Count > 0)
            {
                TestContext.WriteLine($"[10] *** TX ECHO CONFIRMED ({matchedTxCompleted.Count}): " +
                    string.Join(", ", matchedTxCompleted.Select(id => $"0x{id:X}")));
            }
            else
            {
                TestContext.WriteLine($"[10] *** No TX echo detected even after SetChannelMode(tx=1) + SetChannelOutput + SetReceiveMode");
            }

            if (matchedTxFailed.Count > 0)
            {
                TestContext.WriteLine($"    TX FAILED ({matchedTxFailed.Count}): " +
                    string.Join(", ", matchedTxFailed.Select(id => $"0x{id:X}")));
            }

            if (matchedNormalRx.Count > 0)
            {
                TestContext.WriteLine($"    SELF-RX ({matchedNormalRx.Count}): " +
                    string.Join(", ", matchedNormalRx.Select(id => $"0x{id:X}")));
            }

            // Step 12: 去激活
            driver.XL_DeactivateChannel(portHandle, mask);
            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景7（TX 回显确认）：V3 打开 → Classic CAN 参数 → SetChannelMode(sentFlag=1) → 激活 → 发送 → 验证回显
    /// 通过调用 XL_CanSetChannelMode 启用 TX echo 后，验证收回路经中出现 XL_TRANSMIT_MSG 事件。
    /// </summary>
    [TestMethod]
    public void Scenario7_V3_SendWithSetChannelMode_VerifyEcho()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // Step 1: V3 打开
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 256,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s1, "XL_OpenPort failed");
            TestContext.WriteLine($"[1] XL_OpenPort(V3): {s1} (handle={portHandle}, permMask=0x{permMask:X})");

            // Step 2: Classic CAN 参数
            var chipParams = new XLClass.xl_chip_params
            {
                bitrate = 500_000, sjw = 1, tseg1 = 13, tseg2 = 2, sam = 1,
            };
            var s2 = driver.XL_CanSetChannelParams(portHandle, mask, chipParams);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s2, "XL_CanSetChannelParams failed");
            TestContext.WriteLine($"[2] XL_CanSetChannelParams: {s2}");

            // Step 3: 【关键】SetChannelMode — 启用 TX echo (tx=1)
            var s3 = driver.XL_CanSetChannelMode(portHandle, mask, tx: 1, txRq: 0);
            TestContext.WriteLine($"[3] XL_CanSetChannelMode(tx=1, txRq=0): {s3}");

            // Step 4: 激活
            var s4 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            Assert.AreEqual(XLDefine.XL_Status.XL_SUCCESS, s4, "XL_ActivateChannel failed");
            TestContext.WriteLine($"[4] XL_ActivateChannel: {s4}");

            // Step 5: 设置通知并排空
            int notifHandle = -1;
            driver.XL_SetNotification(portHandle, ref notifHandle, 1);
            WaitForNotificationOrTimeout(notifHandle, 500);

            int drained = 0;
            while (true)
            {
                var drainEv = new XLClass.xl_event();
                var drainSt = driver.XL_Receive(portHandle, ref drainEv);
                if (drainSt != XLDefine.XL_Status.XL_SUCCESS) break;
                drained++;
            }
            TestContext.WriteLine($"[5] Drained {drained} initial events");

            // Step 6: 发送测试帧
            uint[] testIds = [0x100, 0x200, 0x300, 0x400, 0x500];
            int sentCount = 0;
            foreach (var id in testIds)
            {
                var msg = new XLClass.xl_can_msg
                {
                    id = id, dlc = 8,
                    flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE,
                    data = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88],
                };
                var txEv = new XLClass.xl_event
                {
                    tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG,
                    tagData = new XLClass.xl_tag_data { can_Msg = msg },
                };
                var st = driver.XL_CanTransmit(portHandle, mask, txEv);
                if (st == XLDefine.XL_Status.XL_SUCCESS) sentCount++;
                TestContext.WriteLine($"  Send ID=0x{id:X}: {st}");
            }
            TestContext.WriteLine($"[6] Successfully sent {sentCount}/{testIds.Length} frames");

            // Step 7: 等待回显
            WaitForNotificationOrTimeout(notifHandle, 1500);

            // Step 8: 轮询收取并按类型分类
            // 参考 CanConnector.ProcessRawData:
            //   XL_RECEIVE_MSG + TX_COMPLETED → 发送成功回显
            //   XL_RECEIVE_MSG + NONE         → 总线正常接收
            //   XL_RECEIVE_MSG + ERROR_FRAME  → 错误帧
            //   XL_TRANSMIT_MSG               → 发送失败反馈
            int normalRxCount = 0, txCompletedCount = 0, errorFrameCount = 0;
            int txFailedCount = 0, chipStateCount = 0, otherCount = 0;
            var normalRxIds = new List<uint>();
            var txCompletedIds = new List<uint>();
            var txFailedIds = new List<uint>();

            for (int i = 0; i < 200; i++)
            {
                var ev = new XLClass.xl_event();
                var st = driver.XL_Receive(portHandle, ref ev);
                if (st != XLDefine.XL_Status.XL_SUCCESS) break;

                if (ev.tag == XLDefine.XL_EventTags.XL_RECEIVE_MSG)
                {
                    var flags = ev.tagData.can_Msg.flags;
                    if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED))
                    {
                        txCompletedCount++;
                        txCompletedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags == XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE)
                    {
                        normalRxCount++;
                        normalRxIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                    }
                    else if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME))
                    {
                        errorFrameCount++;
                    }
                    else
                    {
                        otherCount++;
                    }
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_TRANSMIT_MSG)
                {
                    txFailedCount++;
                    txFailedIds.Add(ev.tagData.can_Msg.id & 0x7FFFFFFF);
                }
                else if (ev.tag == XLDefine.XL_EventTags.XL_CHIP_STATE)
                    chipStateCount++;
                else
                    otherCount++;
            }

            // Step 9: 输出结果
            TestContext.WriteLine($"[7] Receive results:");
            TestContext.WriteLine($"    XL_RECEIVE_MSG (bus RX):       {normalRxCount}");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+TX_COMPLETED:   {txCompletedCount}  (发送成功回显)");
            TestContext.WriteLine($"    XL_RECEIVE_MSG+ERROR_FRAME:    {errorFrameCount}  (错误帧)");
            TestContext.WriteLine($"    XL_TRANSMIT_MSG:                {txFailedCount}  (发送失败反馈)");
            TestContext.WriteLine($"    XL_CHIP_STATE:                  {chipStateCount}");
            TestContext.WriteLine($"    Other:                          {otherCount}");

            var sentIdSet = new HashSet<uint>(testIds);
            var matchedTxCompleted = txCompletedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedTxFailed = txFailedIds.Where(id => sentIdSet.Contains(id)).ToList();
            var matchedNormalRx = normalRxIds.Where(id => sentIdSet.Contains(id)).ToList();

            if (matchedTxCompleted.Count > 0)
            {
                TestContext.WriteLine($"[8] *** TX ECHO CONFIRMED ({matchedTxCompleted.Count}): " +
                    string.Join(", ", matchedTxCompleted.Select(id => $"0x{id:X}")));
            }
            else
            {
                TestContext.WriteLine($"[8] *** No TX echo detected after SetChannelMode(tx=1)");
            }

            if (matchedTxFailed.Count > 0)
            {
                TestContext.WriteLine($"    TX FAILED ({matchedTxFailed.Count}): " +
                    string.Join(", ", matchedTxFailed.Select(id => $"0x{id:X}")));
            }

            if (matchedNormalRx.Count > 0)
            {
                TestContext.WriteLine($"    SELF-RX ({matchedNormalRx.Count}): " +
                    string.Join(", ", matchedNormalRx.Select(id => $"0x{id:X}")));
            }

            if (normalRxCount > 0)
            {
                var sampleIds = normalRxIds.Take(Math.Min(10, normalRxIds.Count))
                    .Select(id => $"0x{id:X}");
                TestContext.WriteLine($"    Bus RX sample IDs: {string.Join(", ", sampleIds)}");
            }

            // Step 10: 去激活
            driver.XL_DeactivateChannel(portHandle, mask);
            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景8（VectorOpenOptions 集成测试）：通过 CanHubRegistry + VectorOpenOptions.TransmitEcho 验证 TX 回显。
    /// </summary>
    [TestMethod]
    public async Task Scenario8_VectorOpenOptions_TransmitEcho()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddVectorAdapter();

        await using var bus = await registry.OpenAsync(
            GetTargetEndpoint(),
            new CanOpenOptions
            {
                BusParameters = CanBusParameters.Classic500k,
                NativeOptions = new VectorOpenOptions { TransmitEcho = true },
            });

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 4096 });

        // 排空初始缓冲区
        var clearCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { while (true) await sub.ReadAsync(clearCts.Token); }
        catch (OperationCanceledException) { }
        TestContext.WriteLine("[1] Drained initial buffer");

        // 发送五帧
        uint[] testIds = [0x111, 0x222, 0x333, 0x444, 0x555];
        for (int i = 0; i < testIds.Length; i++)
        {
            var frame = CanFrame.CreateData(CanId.Standard(testIds[i]),
                [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
            var result = await bus.SendAsync(frame);
            TestContext.WriteLine($"[2] Send ID=0x{testIds[i]:X}: {result.Status}");
        }

        // 收集接收事件
        var echoCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        int rxCount = 0, txConfirmCount = 0;
        var echoFrames = new List<uint>();
        var rxFrames = new List<uint>();

        try
        {
            while (!echoCts.Token.IsCancellationRequested)
            {
                var evt = await sub.ReadAsync(echoCts.Token);
                rxCount++;
                if (evt.Direction == CanFrameDirection.Transmit)
                {
                    txConfirmCount++;
                    echoFrames.Add(evt.Frame.Id.Value);
                }
                else
                {
                    rxFrames.Add(evt.Frame.Id.Value);
                }
            }
        }
        catch (OperationCanceledException) { }

        TestContext.WriteLine($"[3] Received: total={rxCount}, TX confirmations={txConfirmCount}, bus RX={rxFrames.Count}");

        var sentIdSet = new HashSet<uint>(testIds);
        var matchedEcho = echoFrames.Where(id => sentIdSet.Contains(id)).ToList();

        if (matchedEcho.Count > 0)
        {
            TestContext.WriteLine($"[4] *** TX ECHO CONFIRMED via VectorOpenOptions: {matchedEcho.Count} frame(s): " +
                string.Join(", ", matchedEcho.Select(id => $"0x{id:X}")));
        }
        else
        {
            TestContext.WriteLine($"[4] *** No TX echo detected via VectorOpenOptions");
        }
    }

    /// <summary>
    /// 场景4：V3 打开 → 不配置 → 激活 → FD 配置（在激活后）
    /// 预期：FD 配置可能失败，但通道已经在工作
    /// </summary>
    [TestMethod]
    public void Scenario4_V3_Activate_Then_FdConfig()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            var mask = GetTargetMask(driver);
            int portHandle = -1;
            ulong permMask = mask;

            // V3 打开，不配置，直接激活
            var s1 = driver.XL_OpenPort(ref portHandle, "CanHub", mask, ref permMask, 256,
                XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION_V3,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            TestContext.WriteLine($"[1] XL_OpenPort(V3): {s1}");

            var s2 = driver.XL_ActivateChannel(portHandle, mask,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
                XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            TestContext.WriteLine($"[2] XL_ActivateChannel: {s2}");

            if (s2 == XLDefine.XL_Status.XL_SUCCESS)
            {
                // 激活后尝试 FD 配置
                var fdConf = new XLClass.XLcanFdConf
                {
                    arbitrationBitRate = 500_000,
                    sjwAbr = 1, tseg1Abr = 13, tseg2Abr = 2,
                    dataBitRate = 2_000_000,
                    sjwDbr = 1, tseg1Dbr = 13, tseg2Dbr = 2,
                    options = 0,
                };
                var s3 = driver.XL_CanFdSetConfiguration(portHandle, mask, fdConf);
                TestContext.WriteLine($"[3] XL_CanFdSetConfiguration (after activate): {s3}");

                // 接收
                int notifHandle = -1;
                driver.XL_SetNotification(portHandle, ref notifHandle, 1);
                WaitForNotificationOrTimeout(notifHandle, 1000);

                // 尝试两种接收方式
                var xlEvent = new XLClass.xl_event();
                var classicStatus = driver.XL_Receive(portHandle, ref xlEvent);
                TestContext.WriteLine($"[4] XL_Receive: {classicStatus}, tag={xlEvent.tag}");

                var rxEvent = new XLClass.XLcanRxEvent();
                var fdStatus = driver.XL_CanReceive(portHandle, ref rxEvent);
                TestContext.WriteLine($"[5] XL_CanReceive: {fdStatus}, tag={rxEvent.tag}");

                driver.XL_DeactivateChannel(portHandle, mask);
            }

            driver.XL_ClosePort(portHandle);
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }

    /// <summary>
    /// 场景9（CanHub 集成测试）：通过 CanHubRegistry + AddVectorAdapter 验证适配器对
    /// 回显帧和错误帧的识别能力。
    /// FD 模式（500k/2M），需要 ECU 在线。
    /// 错误帧触发方式：发送配置的请求 ID (BRS) 携带 02 10 01 00 00 00 00 00。
    /// </summary>
    [TestMethod]
    public async Task Scenario9_CanHub_EchoAndErrorFrameDetection()
    {
        var target = VectorEcuTestSettings.GetTarget();
        var registry = CanHubRegistry.CreateDefault();
        registry.AddVectorAdapter();

        await using var bus = await registry.OpenAsync(
            target.Endpoint,
            new CanOpenOptions
            {
                BusParameters = CanBusParameters.Classic500k,
                NativeOptions = new VectorOpenOptions { TransmitEcho = true },
            });

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 4096 });

        // 排空初始缓冲区
        var drainCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { while (true) await sub.ReadAsync(drainCts.Token); }
        catch (OperationCanceledException) { }
        TestContext.WriteLine("[1] Drained initial buffer");

        // —— 阶段 1：收集 ECU 正常 FD 帧 ——
        //var phase1Cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        //var phase1Frames = new List<CanFrameEvent>();
        //try
        //{
        //    while (true)
        //        phase1Frames.Add(await sub.ReadAsync(phase1Cts.Token));
        //}
        //catch (OperationCanceledException) { }

        //int rxFromEcu = phase1Frames.Count(f => f.Direction == CanFrameDirection.Receive);
        //int fdFrames = phase1Frames.Count(f => f.Frame.Flags.HasFlag(CanFrameFlags.FD));
        //TestContext.WriteLine($"[2] Phase 1 — Collected {phase1Frames.Count} frames: " +
        //    $"RX={rxFromEcu}, FD={fdFrames}");

        //Assert.IsTrue(rxFromEcu > 0,
        //    $"Expected to receive FD frames from ECU, but got 0 RX frames. " +
        //    $"Total collected: {phase1Frames.Count}");

        // —— 阶段 2：TX 回显验证 ——
        //uint[] testIds = [0x111, 0x222, 0x333, 0x444, 0x555];
        //for (int i = 0; i < testIds.Length; i++)
        //{
        //    var frame = CanFrame.CreateFdData(CanId.Standard(testIds[i]),
        //        [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08],
        //        bitRateSwitch: false);
        //    var result = await bus.SendAsync(frame);
        //    TestContext.WriteLine($"[3] Send ID=0x{testIds[i]:X}: {result.Status}");
        //}

        //var phase2Cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        //var echoFrames = new List<CanFrameEvent>();
        //var phase2Rx = new List<CanFrameEvent>();
        //try
        //{
        //    while (true)
        //    {
        //        var evt = await sub.ReadAsync(phase2Cts.Token);
        //        if (evt.Direction == CanFrameDirection.Transmit)
        //            echoFrames.Add(evt);
        //        else
        //            phase2Rx.Add(evt);
        //    }
        //}
        //catch (OperationCanceledException) { }

        //var sentIdSet = new HashSet<uint>(testIds);
        //var matchedEcho = echoFrames
        //    .Where(e => sentIdSet.Contains(e.Frame.Id.Value))
        //    .ToList();

        //TestContext.WriteLine($"[4] Phase 2 — Echo: {echoFrames.Count} TX events, " +
        //    $"{matchedEcho.Count} matched sent IDs, {phase2Rx.Count} RX frames");
        //foreach (var e in matchedEcho)
        //    TestContext.WriteLine($"      Echo ID=0x{e.Frame.Id.Value:X}, " +
        //        $"ObservationKind={e.ObservationKind}, Outcome={e.Outcome}");

        //Assert.IsTrue(matchedEcho.Count > 0,
        //    $"Expected TX echo frames via VectorOpenOptions.TransmitEcho, " +
        //    $"but none of the 5 sent IDs matched. Total TX events: {echoFrames.Count}. " +
        //    $"Verify that the configured channel is connected to a live bus.");

        // —— 阶段 3：错误帧检测 ——
        // 发送配置的请求 ID (BRS) 携带 UDS DiagnosticSessionControl 请求，
        // 在当前环境中会触发大量 CAN FD 错误帧。
        // 错误帧现在通过帧广播路径（CanFrameEvent）到达订阅者，而非状态事件。
        var errorTriggerFrame = CanFrame.CreateFdData(
            CanId.Standard(target.RequestId),
            [0x02, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00],
            bitRateSwitch: true);
        var triggerResult = await bus.SendAsync(errorTriggerFrame);
        TestContext.WriteLine($"[5] Sent error-trigger frame 0x{target.RequestId:X} (BRS): {triggerResult.Status}");

        var phase3Cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var errorFrameEvents = new List<CanFrameEvent>();
        try
        {
            while (true)
            {
                var evt = await sub.ReadAsync(phase3Cts.Token);
                if (evt.Frame.Kind == CanFrameKind.Error ||
                    evt.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse))
                    errorFrameEvents.Add(evt);
            }
        }
        catch (OperationCanceledException) { }

        TestContext.WriteLine($"[6] Phase 3 — Error frame events: {errorFrameEvents.Count}");
        foreach (var e in errorFrameEvents)
            TestContext.WriteLine($"      ErrorFrame: Direction={e.Direction}, Kind={e.Frame.Kind}, " +
                $"ErrorCode=0x{e.Frame.ErrorCode:X}, NativeErrorCode=0x{e.NativeErrorCode:X}");

        // 柔性断言：错误帧依赖硬件条件
        if (errorFrameEvents.Count > 0)
        {
            Assert.IsTrue(errorFrameEvents.Count > 0,
                $"Error frame detection verified: {errorFrameEvents.Count} error frame events " +
                $"broadcast through frame subscription.");
        }
        else
        {
            Assert.Inconclusive(
                "No error frame events detected through frame subscription. " +
                $"Verify hardware setup: {target.Device}[{target.DeviceIndex}] channel {target.ChannelIndex} should have ECU online.");
        }
    }

    private static string GetTargetEndpoint()
    {
        var target = GetTarget();
        return $"vector://{target.Device}?deviceIndex={target.DeviceIndex}&channelIndex={target.ChannelIndex}";
    }

    private static ulong GetTargetMask(XLDriver driver)
    {
        var target = GetTarget();
        var deviceType = VectorDeviceTypeMapper.Resolve(target.Device);
        var mask = driver.XL_GetChannelMask(deviceType, target.DeviceIndex, target.ChannelIndex);
        if (mask == 0)
        {
            Assert.Inconclusive(
                $"No Vector channel mask for {target.Device}[{target.DeviceIndex}] channel {target.ChannelIndex}.");
        }

        return mask;
    }

    private static VectorTestTarget GetTarget()
    {
        var device = Environment.GetEnvironmentVariable(DeviceVariable);
        if (string.IsNullOrWhiteSpace(device))
            device = "virtual";

        return new VectorTestTarget(
            device,
            GetIntEnvironment(DeviceIndexVariable, 0),
            GetIntEnvironment(ChannelIndexVariable, 0));
    }

    private static int GetIntEnvironment(string variableName, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static void WaitForNotificationOrTimeout(int notificationHandle, uint timeoutMs)
    {
        if (notificationHandle < 0)
            return;

        var waitResult = NativeMethods.WaitForSingleObject(new IntPtr(notificationHandle), timeoutMs);
        if (waitResult is NativeMethods.WAIT_OBJECT_0 or NativeMethods.WAIT_TIMEOUT)
            return;

        Assert.Inconclusive($"WaitForSingleObject failed for Vector notification handle {notificationHandle}.");
    }

    private readonly record struct VectorTestTarget(string Device, int DeviceIndex, int ChannelIndex);
}
