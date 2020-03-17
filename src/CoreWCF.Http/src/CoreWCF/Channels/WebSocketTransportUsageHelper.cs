using System.ComponentModel;

namespace CoreWCF.Channels
{
    static class WebSocketTransportUsageHelper
    {
        internal static bool IsDefined(WebSocketTransportUsage value)
        {
            return value == WebSocketTransportUsage.WhenDuplex
                || value == WebSocketTransportUsage.Never
                || value == WebSocketTransportUsage.Always;
        }

        internal static void Validate(WebSocketTransportUsage value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidEnumArgumentException(nameof(value), (int)value, typeof(WebSocketTransportUsage)));
            }
        }
    }
}