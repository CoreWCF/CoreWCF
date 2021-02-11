// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;

namespace CoreWCF.Channels
{
    public interface ISecurityCapabilities
    {
        ProtectionLevel SupportedRequestProtectionLevel { get; }
        ProtectionLevel SupportedResponseProtectionLevel { get; }
        bool SupportsClientAuthentication { get; }
        bool SupportsClientWindowsIdentity { get; }
        bool SupportsServerAuthentication { get; }
    }
}
