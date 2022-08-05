// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Security;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal static class MsmqDefaults
    {
        internal const MessageCredentialType DefaultClientCredentialType = MessageCredentialType.Windows;
        internal const Uri CustomDeadLetterQueue = null;
        internal const DeadLetterQueue DeadLetterQueue = CoreWCF.Channels.DeadLetterQueue.System;
        internal const bool Durable = true;
        internal const bool ExactlyOnce = true;
        internal const bool ReceiveContextEnabled = true;
        internal const int MaxRetryCycles = 2;
        internal const int MaxPoolSize = 8;
        internal const MsmqAuthenticationMode MsmqAuthenticationMode1 = MsmqAuthenticationMode.WindowsDomain;
        internal const MsmqEncryptionAlgorithm MsmqEncryptionAlgorithm1 = MsmqEncryptionAlgorithm.RC4Stream;
        internal const MsmqSecureHashAlgorithm DefaultMsmqSecureHashAlgorithm = MsmqSecureHashAlgorithm.Sha256;
        internal static MsmqSecureHashAlgorithm MsmqSecureHashAlgorithm = MsmqSecureHashAlgorithm.Sha1;
        internal const ProtectionLevel MsmqProtectionLevel = ProtectionLevel.Sign;
        // internal const ReceiveErrorHandling ReceiveErrorHandling = System.ServiceModel.ReceiveErrorHandling.Fault;
        internal const int ReceiveRetryCount = 5;
        // internal const QueueTransferProtocol QueueTransferProtocol = System.ServiceModel.QueueTransferProtocol.Native;
        internal static TimeSpan RetryCycleDelay { get { return TimeSpan.FromMinutes(30); } }
        internal const string RetryCycleDelayString = "00:30:00";
        internal static TimeSpan TimeToLive { get { return TimeSpan.FromDays(1); } }
        internal const string TimeToLiveString = "1.00:00:00";
        internal const bool UseActiveDirectory = false;
        internal const bool UseSourceJournal = false;
        internal const bool UseMsmqTracing = false;
        internal static TimeSpan ValidityDuration { get { return TimeSpan.FromMinutes(5); } }
        internal const string ValidityDurationString = "00:05:00";
        internal static SecurityAlgorithmSuite MessageSecurityAlgorithmSuite
        {
            get { return SecurityAlgorithmSuite.Default; }
        }
    }
}
