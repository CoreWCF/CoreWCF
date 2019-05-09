using System;

namespace Microsoft.ServiceModel.Channels
{
    public static class TransportDefaults
    {
        internal const long MaxReceivedMessageSize = 65536;
        internal const int MaxBufferSize = (int)MaxReceivedMessageSize;
    }

    public static class BasicHttpBindingDefaults
    {
        public const WSMessageEncoding MessageEncoding = WSMessageEncoding.Text;
    }

    internal static class HttpTransportDefaults
    {
        internal const TransferMode TransferMode = Microsoft.ServiceModel.TransferMode.Buffered;
        internal const bool KeepAliveEnabled = true;
    }

    internal static class ConnectionOrientedTransportDefaults
    {
        internal const int ConnectionBufferSize = 8192;
    }
}