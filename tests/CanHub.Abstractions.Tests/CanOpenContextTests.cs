using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanOpenContextTests
{
    [TestMethod(DisplayName = "正常构造设置所有属性")]
    public void Constructor_SetsProperties()
    {
        var endpoint = CanEndpoint.Parse("virtual://bench?channel=0");
        var options = new CanOpenOptions();
        var ctx = new CanOpenContext(endpoint, options);

        Assert.AreSame(endpoint, ctx.Endpoint);
        Assert.AreSame(options, ctx.Options);
    }

    [TestMethod(DisplayName = "endpoint为null时抛出ArgumentNullException")]
    public void Constructor_NullEndpoint_ThrowsArgumentNullException()
    {
        var options = new CanOpenOptions();
        TestAssert.Throws<ArgumentNullException>(() => new CanOpenContext(null!, options));
    }

    [TestMethod(DisplayName = "options为null时抛出ArgumentNullException")]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var endpoint = CanEndpoint.Parse("virtual://bench?channel=0");
        TestAssert.Throws<ArgumentNullException>(() => new CanOpenContext(endpoint, null!));
    }
}
