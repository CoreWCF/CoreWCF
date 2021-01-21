// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Dispatcher
{
    internal class SharedRuntimeState
    {
        private bool _isImmutable;

        internal SharedRuntimeState(bool isOnServer)
        {
            IsOnServer = isOnServer;
        }

        internal bool EnableFaults { get; set; } = true;

        internal bool IsOnServer { get; }

        internal bool ManualAddressing { get; set; }

        internal bool ValidateMustUnderstand { get; set; } = true;

        internal void LockDownProperties()
        {
            _isImmutable = true;
        }

        internal void ThrowIfImmutable()
        {
            if (_isImmutable)
            {
                if (IsOnServer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxImmutableServiceHostBehavior0));
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxImmutableChannelFactoryBehavior0));
            }
        }
    }

}