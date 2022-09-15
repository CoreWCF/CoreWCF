// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    //TODO : - Remove this file ?
    internal static class ServiceDefaults
    {
        internal static TimeSpan CloseTimeout => TimeSpan.FromMinutes(1);
        internal const string CloseTimeoutString = "00:01:00";
        internal static TimeSpan OpenTimeout => TimeSpan.FromMinutes(1);
        internal const string OpenTimeoutString = "00:01:00";
        internal static TimeSpan ReceiveTimeout => TimeSpan.FromMinutes(10);
        internal const string ReceiveTimeoutString = "00:10:00";
        internal static TimeSpan SendTimeout => TimeSpan.FromMinutes(1);
        internal const string SendTimeoutString = "00:01:00";
    }
}
