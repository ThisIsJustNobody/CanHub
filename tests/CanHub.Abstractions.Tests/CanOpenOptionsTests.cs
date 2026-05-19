using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanOpenOptionsTests
{
    [TestMethod(DisplayName = "默认总线参数为Classic500k")]
    public void Default_BusParameters_IsClassic500k()
    {
        var opts = new CanOpenOptions();
        Assert.AreEqual(CanBusParameters.Classic500k, opts.BusParameters);
    }

    [TestMethod(DisplayName = "默认NativeOptions为null")]
    public void Default_NativeOptions_IsNull()
    {
        var opts = new CanOpenOptions();
        Assert.IsNull(opts.NativeOptions);
    }

    [TestMethod(DisplayName = "默认恢复策略为禁用")]
    public void Default_Recovery_IsDisabled()
    {
        var opts = new CanOpenOptions();

        Assert.AreSame(CanRecoveryOptions.Disabled, opts.Recovery);
        Assert.AreEqual(CanRecoveryMode.Disabled, opts.Recovery.Mode);
    }

    [TestMethod(DisplayName = "Recovery 赋值 null 时抛出异常")]
    public void SetRecovery_Null_ThrowsArgumentNullException()
    {
        var opts = new CanOpenOptions();

        TestAssert.Throws<ArgumentNullException>(() => opts.Recovery = null!);
    }

    [TestMethod(DisplayName = "ResetOnFault 默认执行一次立即重开")]
    public void ResetOnFault_Defaults_ToImmediateSingleAttempt()
    {
        var recovery = CanRecoveryOptions.ResetOnFault();

        Assert.AreEqual(CanRecoveryMode.ResetOnFault, recovery.Mode);
        Assert.AreEqual(CanRecoveryTrigger.BusOff, recovery.Triggers);
        Assert.AreEqual(TimeSpan.Zero, recovery.FaultDwellTime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), recovery.RestartDelay);
        Assert.AreEqual(1, recovery.MaxAttempts);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), recovery.MaxBackoffDelay);
        Assert.IsTrue(recovery.RejectTransmitsWhileRecovering);
    }

    [TestMethod(DisplayName = "ReopenWithBackoff 默认带退避和三次尝试")]
    public void ReopenWithBackoff_Defaults_ToThreeAttemptsWithBackoff()
    {
        var recovery = CanRecoveryOptions.ReopenWithBackoff();

        Assert.AreEqual(CanRecoveryMode.ReopenWithBackoff, recovery.Mode);
        Assert.AreEqual(CanRecoveryTrigger.BusOff, recovery.Triggers);
        Assert.AreEqual(TimeSpan.Zero, recovery.FaultDwellTime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), recovery.RestartDelay);
        Assert.AreEqual(3, recovery.MaxAttempts);
        Assert.AreEqual(TimeSpan.FromSeconds(5), recovery.MaxBackoffDelay);
        Assert.IsTrue(recovery.RejectTransmitsWhileRecovering);
    }
}
