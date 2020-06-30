
using System;

namespace CoreWCF.Security
{
    interface IListenerSecureConversationSessionSettings
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
