﻿using System.Net.Security;

namespace CoreWCF.Channels
{
    class SecurityCapabilities : ISecurityCapabilities
    {
        public SecurityCapabilities(bool supportsClientAuth, bool supportsServerAuth, bool supportsClientWindowsIdentity,
            ProtectionLevel requestProtectionLevel, ProtectionLevel responseProtectionLevel)
        {
            SupportsClientAuthentication = supportsClientAuth;
            SupportsServerAuthentication = supportsServerAuth;
            SupportsClientWindowsIdentity = supportsClientWindowsIdentity;
            SupportedRequestProtectionLevel = requestProtectionLevel;
            SupportedResponseProtectionLevel = responseProtectionLevel;
        }

        public ProtectionLevel SupportedRequestProtectionLevel { get; }
        public ProtectionLevel SupportedResponseProtectionLevel { get; }
        public bool SupportsClientAuthentication { get; }
        public bool SupportsClientWindowsIdentity { get; }
        public bool SupportsServerAuthentication { get; }

        static SecurityCapabilities None => new SecurityCapabilities(false, false, false, ProtectionLevel.None, ProtectionLevel.None);

        internal static bool IsEqual(ISecurityCapabilities capabilities1, ISecurityCapabilities capabilities2)
        {
            if (capabilities1 == null)
            {
                capabilities1 = SecurityCapabilities.None;
            }

            if (capabilities2 == null)
            {
                capabilities2 = SecurityCapabilities.None;
            }

            if (capabilities1.SupportedRequestProtectionLevel != capabilities2.SupportedRequestProtectionLevel)
            {
                return false;
            }

            if (capabilities1.SupportedResponseProtectionLevel != capabilities2.SupportedResponseProtectionLevel)
            {
                return false;
            }

            if (capabilities1.SupportsClientAuthentication != capabilities2.SupportsClientAuthentication)
            {
                return false;
            }

            if (capabilities1.SupportsClientWindowsIdentity != capabilities2.SupportsClientWindowsIdentity)
            {
                return false;
            }

            if (capabilities1.SupportsServerAuthentication != capabilities2.SupportsServerAuthentication)
            {
                return false;
            }

            return true;
        }
    }
}