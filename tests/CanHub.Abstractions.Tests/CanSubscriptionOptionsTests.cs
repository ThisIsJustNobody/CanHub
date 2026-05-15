using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanSubscriptionOptionsTests
{
    [TestMethod(DisplayName = "默认容量4096且满载丢弃最旧帧")]
    public void Defaults_Capacity4096_FullModeDropOldest()
    {
        var options = new CanSubscriptionOptions();

        Assert.AreEqual(4096, options.QueueCapacity);
        Assert.AreEqual(CanQueueFullMode.DropOldest, options.FullMode);
    }

    [TestMethod(DisplayName = "设置容量改变值")]
    public void SetCapacity_ChangesValue()
    {
        var options = new CanSubscriptionOptions { QueueCapacity = 256 };
        Assert.AreEqual(256, options.QueueCapacity);
    }

    [TestMethod(DisplayName = "设置满载模式改变值")]
    public void SetFullMode_ChangesValue()
    {
        var options = new CanSubscriptionOptions { FullMode = CanQueueFullMode.DropNewest };
        Assert.AreEqual(CanQueueFullMode.DropNewest, options.FullMode);
    }

}
