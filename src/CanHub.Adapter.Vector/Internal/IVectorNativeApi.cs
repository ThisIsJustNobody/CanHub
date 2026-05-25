using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

internal interface IVectorNativeApi
{
    string GetErrorString(XLDefine.XL_Status status);

    ulong GetChannelMask(XLDefine.XL_HardwareType deviceType, int deviceIndex, int channelIndex);

    XLDefine.XL_Status OpenPort(
        ref int portHandle,
        string userName,
        ulong accessMask,
        ref ulong permissionMask,
        uint rxQueueSize,
        XLDefine.XL_InterfaceVersion interfaceVersion,
        XLDefine.XL_BusTypes busType);

    XLDefine.XL_Status CanFdSetConfiguration(
        int portHandle,
        ulong accessMask,
        XLClass.XLcanFdConf fdConf);

    XLDefine.XL_Status CanSetChannelParams(
        int portHandle,
        ulong accessMask,
        XLClass.xl_chip_params chipParams);

    XLDefine.XL_Status CanSetChannelOutput(
        int portHandle,
        ulong accessMask,
        XLDefine.XL_OutputMode outputMode);

    XLDefine.XL_Status CanSetChannelMode(
        int portHandle,
        ulong accessMask,
        uint tx,
        uint txRq);

    XLDefine.XL_Status CanSetReceiveMode(
        int portHandle,
        byte errorFrame,
        byte chipState);

    XLDefine.XL_Status SetNotification(int portHandle, ref int handle, int queueLevel);

    XLDefine.XL_Status ActivateChannel(
        int portHandle,
        ulong accessMask,
        XLDefine.XL_BusTypes busType,
        XLDefine.XL_AC_Flags flags);

    XLDefine.XL_Status DeactivateChannel(int portHandle, ulong accessMask);

    XLDefine.XL_Status ClosePort(int portHandle);

    XLDefine.XL_Status ReceiveClassic(int portHandle, ref XLClass.xl_event ev);

    XLDefine.XL_Status CanReceive(int portHandle, ref XLClass.XLcanRxEvent rx);

    XLDefine.XL_Status CanTransmit(int portHandle, ulong accessMask, XLClass.xl_event ev);

    XLDefine.XL_Status CanTransmitEx(
        int portHandle,
        ulong accessMask,
        ref uint sent,
        XLClass.XLcanTxEvent tx);
}

internal sealed class VectorNativeApi : IVectorNativeApi
{
    public string GetErrorString(XLDefine.XL_Status status) =>
        VectorDriver.Driver.XL_GetErrorString(status);

    public ulong GetChannelMask(XLDefine.XL_HardwareType deviceType, int deviceIndex, int channelIndex) =>
        VectorDriver.Driver.XL_GetChannelMask(deviceType, deviceIndex, channelIndex);

    public XLDefine.XL_Status OpenPort(
        ref int portHandle,
        string userName,
        ulong accessMask,
        ref ulong permissionMask,
        uint rxQueueSize,
        XLDefine.XL_InterfaceVersion interfaceVersion,
        XLDefine.XL_BusTypes busType) =>
        VectorDriver.Driver.XL_OpenPort(
            ref portHandle,
            userName,
            accessMask,
            ref permissionMask,
            rxQueueSize,
            interfaceVersion,
            busType);

    public XLDefine.XL_Status CanFdSetConfiguration(
        int portHandle,
        ulong accessMask,
        XLClass.XLcanFdConf fdConf) =>
        VectorDriver.Driver.XL_CanFdSetConfiguration(portHandle, accessMask, fdConf);

    public XLDefine.XL_Status CanSetChannelParams(
        int portHandle,
        ulong accessMask,
        XLClass.xl_chip_params chipParams) =>
        VectorDriver.Driver.XL_CanSetChannelParams(portHandle, accessMask, chipParams);

    public XLDefine.XL_Status CanSetChannelOutput(
        int portHandle,
        ulong accessMask,
        XLDefine.XL_OutputMode outputMode) =>
        VectorDriver.Driver.XL_CanSetChannelOutput(portHandle, accessMask, outputMode);

    public XLDefine.XL_Status CanSetChannelMode(
        int portHandle,
        ulong accessMask,
        uint tx,
        uint txRq) =>
        VectorDriver.Driver.XL_CanSetChannelMode(portHandle, accessMask, tx, txRq);

    public XLDefine.XL_Status CanSetReceiveMode(
        int portHandle,
        byte errorFrame,
        byte chipState) =>
        VectorDriver.Driver.XL_CanSetReceiveMode(portHandle, errorFrame, chipState);

    public XLDefine.XL_Status SetNotification(int portHandle, ref int handle, int queueLevel) =>
        VectorDriver.Driver.XL_SetNotification(portHandle, ref handle, queueLevel);

    public XLDefine.XL_Status ActivateChannel(
        int portHandle,
        ulong accessMask,
        XLDefine.XL_BusTypes busType,
        XLDefine.XL_AC_Flags flags) =>
        VectorDriver.Driver.XL_ActivateChannel(portHandle, accessMask, busType, flags);

    public XLDefine.XL_Status DeactivateChannel(int portHandle, ulong accessMask) =>
        VectorDriver.Driver.XL_DeactivateChannel(portHandle, accessMask);

    public XLDefine.XL_Status ClosePort(int portHandle) =>
        VectorDriver.Driver.XL_ClosePort(portHandle);

    public XLDefine.XL_Status ReceiveClassic(int portHandle, ref XLClass.xl_event ev) =>
        VectorDriver.Driver.XL_Receive(portHandle, ref ev);

    public XLDefine.XL_Status CanReceive(int portHandle, ref XLClass.XLcanRxEvent rx) =>
        VectorDriver.Driver.XL_CanReceive(portHandle, ref rx);

    public XLDefine.XL_Status CanTransmit(int portHandle, ulong accessMask, XLClass.xl_event ev) =>
        VectorDriver.Driver.XL_CanTransmit(portHandle, accessMask, ev);

    public XLDefine.XL_Status CanTransmitEx(
        int portHandle,
        ulong accessMask,
        ref uint sent,
        XLClass.XLcanTxEvent tx) =>
        VectorDriver.Driver.XL_CanTransmitEx(portHandle, accessMask, ref sent, tx);
}
