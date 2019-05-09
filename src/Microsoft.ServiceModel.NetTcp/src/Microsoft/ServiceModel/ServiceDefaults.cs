using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel
{
    internal static class ServiceDefaults
    {
        //internal static TimeSpan ServiceHostCloseTimeout { get { return TimeSpanHelper.FromSeconds(10, ServiceHostCloseTimeoutString); } }
        //internal static TimeSpan ServiceHostCloseTimeout => TimeSpan.FromSeconds(10);
        //internal const string ServiceHostCloseTimeoutString = "00:00:10";
        //internal static TimeSpan CloseTimeout { get { return TimeSpanHelper.FromMinutes(1, CloseTimeoutString); } }
        internal static TimeSpan CloseTimeout => TimeSpan.FromMinutes(1);
        internal const string CloseTimeoutString = "00:01:00";
        //internal static TimeSpan OpenTimeout { get { return TimeSpanHelper.FromMinutes(1, OpenTimeoutString); } }
        internal static TimeSpan OpenTimeout => TimeSpan.FromMinutes(1);
        internal const string OpenTimeoutString = "00:01:00";
        //internal static TimeSpan ReceiveTimeout { get { return TimeSpanHelper.FromMinutes(10, ReceiveTimeoutString); } }
        internal static TimeSpan ReceiveTimeout => TimeSpan.FromMinutes(10);
        internal const string ReceiveTimeoutString = "00:10:00";
        //internal static TimeSpan SendTimeout { get { return TimeSpanHelper.FromMinutes(1, SendTimeoutString); } }
        internal static TimeSpan SendTimeout => TimeSpan.FromMinutes(1);
        internal const string SendTimeoutString = "00:01:00";
        //internal static TimeSpan TransactionTimeout { get { return TimeSpanHelper.FromMinutes(1, TransactionTimeoutString); } }
        //internal const string TransactionTimeoutString = "00:00:00";
    }
}
