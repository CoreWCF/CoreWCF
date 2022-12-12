// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Channels
{
    public interface IStreamUpgradeChannelBindingProvider : IChannelBindingProvider
    {
        ChannelBinding GetChannelBinding(StreamUpgradeAcceptor upgradeAcceptor, ChannelBindingKind kind);
    }
}
