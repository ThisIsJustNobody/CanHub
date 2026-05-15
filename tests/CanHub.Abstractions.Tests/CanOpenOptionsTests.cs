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
}
