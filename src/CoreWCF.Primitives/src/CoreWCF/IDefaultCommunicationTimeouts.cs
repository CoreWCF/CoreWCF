// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    public interface IDefaultCommunicationTimeouts
    {
        TimeSpan CloseTimeout { get; }
        TimeSpan OpenTimeout { get; }
        TimeSpan ReceiveTimeout { get; }
        TimeSpan SendTimeout { get; }
    }
}