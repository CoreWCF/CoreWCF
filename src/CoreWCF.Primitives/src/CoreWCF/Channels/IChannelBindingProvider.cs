// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    // TODO: Make internal again
    public interface IChannelBindingProvider
    {
        void EnableChannelBindingSupport();
        bool IsChannelBindingSupportEnabled { get; }
    }
}
