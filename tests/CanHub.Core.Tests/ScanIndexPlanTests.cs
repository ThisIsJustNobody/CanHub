using CanHub.Core;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class ScanIndexPlanTests
{
    [TestMethod]
    public void DefaultOptions_StopAfterFirstMiss()
    {
        var plan = ScanIndexPlan.FromOptions(new ScanOptions());

        Assert.AreEqual(0, plan.StartIndex);
        Assert.IsFalse(plan.ShouldContinueAfter(scannedCount: 1, foundAtCurrentIndex: false));
    }

    [TestMethod]
    public void DefaultOptions_ContinueAfterFoundUntilMiss()
    {
        var plan = ScanIndexPlan.FromOptions(new ScanOptions());

        Assert.IsTrue(plan.ShouldContinueAfter(scannedCount: 1, foundAtCurrentIndex: true));
        Assert.IsFalse(plan.ShouldContinueAfter(scannedCount: 2, foundAtCurrentIndex: false));
    }

    [TestMethod]
    public void MinDepth_ContinuesThroughMissesUntilMinimumDepth()
    {
        var plan = ScanIndexPlan.FromOptions(new ScanOptions { MinDepth = 2 });

        Assert.IsTrue(plan.ShouldContinueAfter(scannedCount: 1, foundAtCurrentIndex: false));
        Assert.IsFalse(plan.ShouldContinueAfter(scannedCount: 2, foundAtCurrentIndex: false));
    }

    [TestMethod]
    public void MinDepth_ContinuesPastMinimumWhenDeviceWasFound()
    {
        var plan = ScanIndexPlan.FromOptions(new ScanOptions { MinDepth = 2, StartIndex = 3 });

        Assert.AreEqual(3, plan.StartIndex);
        Assert.IsTrue(plan.ShouldContinueAfter(scannedCount: 2, foundAtCurrentIndex: true));
        Assert.IsFalse(plan.ShouldContinueAfter(scannedCount: 3, foundAtCurrentIndex: false));
    }
}
