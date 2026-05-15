using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorDeviceTypeMapperTests
{
    [TestMethod]
    [DataRow("virtual", XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL)]
    [DataRow("VIRTUAL", XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL)]
    [DataRow("Virtual", XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL)]
    public void Resolve_ExactMatch_ReturnsType(string input, XLDefine.XL_HardwareType expected)
    {
        var result = VectorDeviceTypeMapper.Resolve(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Resolve_SuffixMatch_VN1630()
    {
        var result = VectorDeviceTypeMapper.Resolve("VN1630");
        Assert.AreEqual(XLDefine.XL_HardwareType.XL_HWTYPE_VN1630, result);
    }

    [TestMethod]
    public void Resolve_ContainsMatch_5610()
    {
        var result = VectorDeviceTypeMapper.Resolve("5610");
        Assert.AreEqual(XLDefine.XL_HardwareType.XL_HWTYPE_VN5610A, result);
    }

    [TestMethod]
    public void TryResolve_ValidInput_ReturnsTrue()
    {
        Assert.IsTrue(VectorDeviceTypeMapper.TryResolve("virtual", out var type));
        Assert.AreEqual(XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL, type);
    }

    [TestMethod]
    public void TryResolve_InvalidInput_ReturnsFalse()
    {
        Assert.IsFalse(VectorDeviceTypeMapper.TryResolve("nonexistent_device", out _));
    }

    [TestMethod]
    public void Resolve_InvalidInput_ThrowsCanException()
    {
        var ex = Assert.ThrowsExactly<CanException>(
            () => VectorDeviceTypeMapper.Resolve("nonexistent_device"));
        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }
}
