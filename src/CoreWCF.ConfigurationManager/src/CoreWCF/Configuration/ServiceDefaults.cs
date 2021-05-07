// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Configuration
{
    static class ServiceDefaults
    {
        internal const string ServiceHostCloseTimeoutString = "00:00:10";
        internal const string CloseTimeoutString = "00:01:00";
        internal const string OpenTimeoutString = "00:01:00";
        internal const string ReceiveTimeoutString = "00:10:00";
        internal const string SendTimeoutString = "00:01:00";
        internal const string TransactionTimeoutString = "00:00:00";
    }
}
