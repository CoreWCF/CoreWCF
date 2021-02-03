// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    public interface IAnonymousUriPrefixMatcher
    {
        void Register(Uri anonymousUriPrefix);
    }
}