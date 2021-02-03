// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security
{
    internal interface IServiceDispatcherSecureConversationSessionSettings
    {
        bool TolerateTransportFailures
        {
            get;
            set;
        }

        int MaximumPendingSessions
        {
            get;
            set;
        }

        TimeSpan InactivityTimeout
        {
            get;
            set;
        }

        TimeSpan MaximumKeyRenewalInterval
        {
            get;
            set;
        }

        TimeSpan KeyRolloverInterval
        {
            get;
            set;
        }

        int MaximumPendingKeysPerSession
        {
            get;
            set;
        }
    }
}
