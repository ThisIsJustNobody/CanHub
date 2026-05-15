namespace CanHub.Abstractions.Tests;

[TestClass]
public sealed class CanCapabilityTests
{
    [TestMethod]
    public void Constructor_SetsProperties()
    {
        var cap = new CanCapability("can-fd", true, "CAN FD support");

        Assert.AreEqual("can-fd", cap.Name);
        Assert.IsTrue(cap.IsRequired);
        Assert.AreEqual("CAN FD support", cap.Description);
    }

    [TestMethod]
    public void Constructor_OptionalDescription_NullByDefault()
    {
        var cap = new CanCapability("bus-off-recovery", false);

        Assert.AreEqual("bus-off-recovery", cap.Name);
        Assert.IsFalse(cap.IsRequired);
        Assert.IsNull(cap.Description);
    }

    [TestMethod]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new CanCapability(null!, false));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new CanCapability(name, false));
    }
}
