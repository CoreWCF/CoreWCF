// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    internal static class SecurityUtils
    {
        internal static EndpointIdentity CreateWindowsIdentity(NetworkCredential serverCredential)
        {
            if (serverCredential != null && !NetworkCredentialHelper.IsDefault(serverCredential))
            {
                string upn;
                if (serverCredential.Domain != null && serverCredential.Domain.Length > 0)
                {
                    upn = serverCredential.UserName + "@" + serverCredential.Domain;
                }
                else
                {
                    upn = serverCredential.UserName;
                }
                return new UpnEndpointIdentity(upn);
            }
            else
            {
                return CreateWindowsIdentity();
            }
        }

        internal static EndpointIdentity CreateWindowsIdentity()
        {
            EndpointIdentity identity = null;
            using (WindowsIdentity self = WindowsIdentity.GetCurrent())
            {
                identity = new SpnEndpointIdentity(string.Format(CultureInfo.InvariantCulture, "host/{0}", DnsCache.MachineName));
            }

            return identity;
        }

        private static class NetworkCredentialHelper
        {
            internal static bool IsDefault(NetworkCredential credential)
            {
                return UnsafeGetDefaultNetworkCredentials().Equals(credential);
            }

            private static NetworkCredential UnsafeGetDefaultNetworkCredentials()
            {
                return CredentialCache.DefaultNetworkCredentials;
            }
        }

        internal static void AddPosixClaims(ClaimsIdentity claimsIdentity, string groupName, uint groupId, uint processId)
        {
            AddPosixGroupIdentityClaim(claimsIdentity, groupName, groupId);
            AddProcessIdClaim(claimsIdentity, processId);
        }

        private static void AddPosixGroupIdentityClaim(ClaimsIdentity claimsIdentity, string groupName, uint groupId)
        {
            claimsIdentity.AddClaim(new Claim(CoreWCF.IdentityModel.Claims.ClaimTypes.PosixGroupId, groupId.ToString()));
            claimsIdentity.AddClaim(new Claim(CoreWCF.IdentityModel.Claims.ClaimTypes.PosixGroupName, groupName));
        }

        internal static void AddProcessIdClaim(ClaimsIdentity claimsIdentity, uint processId)
        {
            claimsIdentity.AddClaim(new Claim(CoreWCF.IdentityModel.Claims.ClaimTypes.ProcessId, processId.ToString()));
        }
    }
}

