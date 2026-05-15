using CanHub;
using CanHub.Adapter.Vector;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class OpenChannelTests
{
    [TestInitialize]
    public void CheckHardwareAvailable()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CANHUB_TEST_VECTOR")))
            Assert.Inconclusive("Skipping: CANHUB_TEST_VECTOR is not set.");
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Diag_TryOpenChannel3()
    {
        var driver = new XLDriver();
        driver.XL_OpenDriver();

        try
        {
            // Channel 3: mask=0x4, V4 for FD
            ulong mask = 0x4;
            ulong permMask = mask;
            int portHandle = -1;

            var status = driver.XL_OpenPort(
                ref portHandle, "CanHub", mask, ref permMask,
                65536, XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION_V4,
                XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            TestContext.WriteLine($"XL_OpenPort(V4, mask=0x{mask:X}): {status} ({(int)status}), handle={portHandle}");

            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                var errorStr = driver.XL_GetErrorString(status);
                TestContext.WriteLine($"Error: {errorStr}");
            }

            if (status == XLDefine.XL_Status.XL_SUCCESS && portHandle >= 0)
            {
                // 激活
                var actStatus = driver.XL_ActivateChannel(portHandle, mask,
                    XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN, XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
                TestContext.WriteLine($"XL_ActivateChannel: {actStatus}");

                if (actStatus == XLDefine.XL_Status.XL_SUCCESS)
                {
                    // 设置通知
                    int notifHandle = -1;
                    driver.XL_SetNotification(portHandle, ref notifHandle, 1);
                    TestContext.WriteLine($"XL_SetNotification: handle={notifHandle}");

                    // 尝试接收
                    var rxEvent = new XLClass.XLcanRxEvent();
                    var rxStatus = driver.XL_CanReceive(portHandle, ref rxEvent);
                    TestContext.WriteLine($"XL_CanReceive: {rxStatus}, tag={rxEvent.tag}");

                    if (rxStatus == XLDefine.XL_Status.XL_SUCCESS)
                    {
                        TestContext.WriteLine($"  canId=0x{rxEvent.tagData.canRxOkMsg.canId:X}, dlc={rxEvent.tagData.canRxOkMsg.dlc}");
                    }

                    driver.XL_DeactivateChannel(portHandle, mask);
                }

                driver.XL_ClosePort(portHandle);
            }
        }
        finally
        {
            driver.XL_CloseDriver();
        }
    }
}
