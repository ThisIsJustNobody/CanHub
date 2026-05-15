namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class ScanOptionsTests
{
    [TestMethod]
    public void DefaultValues()
    {
        var opts = new ScanOptions();

        Assert.AreEqual(0, opts.MinDepth);
        Assert.AreEqual(0, opts.StartIndex);
    }

    [TestMethod]
    public void CustomValues()
    {
        var opts = new ScanOptions { MinDepth = 4, StartIndex = 2 };

        Assert.AreEqual(4, opts.MinDepth);
        Assert.AreEqual(2, opts.StartIndex);
    }
}
