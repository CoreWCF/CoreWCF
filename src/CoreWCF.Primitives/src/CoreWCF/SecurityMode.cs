namespace CoreWCF
{
    using System;
    public enum SecurityMode
    {
        None = 0,
        Transport = 1,
        Message = 2,
        TransportWithMessageCredential = 3,
    }
    [Flags]
    internal enum UnifiedSecurityMode
    {
        None = 0x001,
        Transport = 0x004,
        Message = 0x008,
        Both = 0x010,
        TransportWithMessageCredential = 0x020,
        TransportCredentialOnly = 0x040,
    }
   public static class SecurityModeHelper
    {
        public static bool IsDefined(SecurityMode value)
        {
            return (value == SecurityMode.None ||
                value == SecurityMode.Transport ||
                value == SecurityMode.Message ||
                value == SecurityMode.TransportWithMessageCredential);
        }

        internal static SecurityMode ToSecurityMode(UnifiedSecurityMode value)
        {
            switch (value)
            {
                case UnifiedSecurityMode.None:
                    return SecurityMode.None;
                case UnifiedSecurityMode.Transport:
                    return SecurityMode.Transport;
                case UnifiedSecurityMode.Message:
                    return SecurityMode.Message;
                case UnifiedSecurityMode.TransportWithMessageCredential:
                    return SecurityMode.TransportWithMessageCredential;
                default:
                    return (SecurityMode)value;
            }
        }
    }
}