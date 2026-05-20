using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanExceptionTests
{
    #region Basic Construction

    [TestMethod(DisplayName = "构造函数设置适配器ID和类别属性")]
    public void Constructor_AdapterIdAndCategory_SetsProperties()
    {
        var ex = new CanException("adapter-1", CanErrorCategory.AdapterNotFound);

        Assert.AreEqual("adapter-1", ex.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterNotFound, ex.Category);
        Assert.IsNull(ex.Endpoint);
        Assert.IsNull(ex.NativeFunction);
        Assert.IsNull(ex.VendorCode);
        Assert.AreEqual(CanRecoverability.Fatal, ex.Recoverability);
    }

    [TestMethod(DisplayName = "默认可恢复性为致命")]
    public void Constructor_DefaultRecoverability_IsFatal()
    {
        var ex = new CanException("a", CanErrorCategory.AdapterError);
        Assert.AreEqual(CanRecoverability.Fatal, ex.Recoverability);
    }

    [TestMethod(DisplayName = "自动消息包含类别和适配器ID")]
    public void Constructor_AutoMessage_ContainsCategoryAndAdapterId()
    {
        var ex = new CanException("my-adapter", CanErrorCategory.InvalidEndpoint);
        Assert.IsTrue(ex.Message.Contains("InvalidEndpoint"), $"Message: {ex.Message}");
        Assert.IsTrue(ex.Message.Contains("my-adapter"), $"Message: {ex.Message}");
    }

    #endregion

    #region Full Construction

    [TestMethod(DisplayName = "全参数构造函数设置所有属性")]
    public void Constructor_AllParameters_SetsAllProperties()
    {
        var ex = new CanException(
            "adapter-2",
            CanErrorCategory.AdapterError,
            endpoint: null,
            nativeFunction: "SendFrame",
            vendorCode: 0x0042,
            recoverability: CanRecoverability.Retryable,
            hint: "Check driver",
            details: new Dictionary<string, string>
            {
                ["endpoint"] = "vector://VN1630?deviceIndex=0&channelIndex=1",
            });

        Assert.AreEqual("adapter-2", ex.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, ex.Category);
        Assert.IsNull(ex.Endpoint);
        Assert.AreEqual("SendFrame", ex.NativeFunction);
        Assert.AreEqual(0x0042, ex.VendorCode);
        Assert.AreEqual(CanRecoverability.Retryable, ex.Recoverability);
        Assert.AreEqual("Check driver", ex.Hint);
        Assert.AreEqual("vector://VN1630?deviceIndex=0&channelIndex=1", ex.Details["endpoint"]);
    }

    [TestMethod(DisplayName = "可选参数为null时正常接受")]
    public void Constructor_NullOptionalParameters_AcceptsNulls()
    {
        var ex = new CanException(
            "a",
            CanErrorCategory.InvalidEndpoint,
            endpoint: null,
            nativeFunction: null,
            vendorCode: null);

        Assert.IsNull(ex.Endpoint);
        Assert.IsNull(ex.NativeFunction);
        Assert.IsNull(ex.VendorCode);
    }

    #endregion

    #region Wrapping Constructor

    [TestMethod(DisplayName = "带消息和内部异常的构造函数")]
    public void Constructor_WithMessageAndInnerException_SetsExceptionProperties()
    {
        var inner = new InvalidOperationException("native failure");
        var ex = new CanException(
            "adapter-3",
            CanErrorCategory.AdapterError,
            "Something went wrong",
            inner);

        Assert.AreEqual("adapter-3", ex.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, ex.Category);
        Assert.AreEqual("Something went wrong", ex.Message);
        Assert.AreSame(inner, ex.InnerException);
        Assert.AreEqual(CanRecoverability.Fatal, ex.Recoverability);
    }

    [TestMethod(DisplayName = "包装异常默认类别和可恢复性")]
    public void Constructor_WrappingException_DefaultsCategoryAndRecoverability()
    {
        var inner = new TimeoutException("timeout");
        var ex = new CanException(
            "adapter-4",
            CanErrorCategory.AdapterNotFound,
            "Connection lost",
            inner);

        Assert.AreEqual(CanErrorCategory.AdapterNotFound, ex.Category);
        Assert.AreEqual(CanRecoverability.Fatal, ex.Recoverability);
    }

    #endregion

    #region Inherits Exception

    [TestMethod(DisplayName = "CanException可被捕获为Exception")]
    public void CanException_CanBeCaughtAsException()
    {
        Exception ex = new CanException("a", CanErrorCategory.AdapterError);
        try
        {
            throw ex;
        }
        catch (CanException canEx)
        {
            Assert.AreEqual(CanErrorCategory.AdapterError, canEx.Category);
        }
    }

    #endregion
}
