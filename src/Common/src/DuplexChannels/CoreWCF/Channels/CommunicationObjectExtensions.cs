// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal static class CommunicationObjectExtensions
    {
        internal static bool DoneReceivingInCurrentState(this CommunicationObject communicationObject)
        {
            communicationObject.ThrowPending();

            switch (communicationObject.State)
            {
                case CommunicationState.Created:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(communicationObject), Guid.Empty, communicationObject);

                case CommunicationState.Opening:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(communicationObject), Guid.Empty, communicationObject);

                case CommunicationState.Opened:
                    return false;

                case CommunicationState.Closing:
                    return true;

                case CommunicationState.Closed:
                    return true;

                case CommunicationState.Faulted:
                    return true;

                default:
                    throw Fx.AssertAndThrow("DoneReceivingInCurrentState: Unknown CommunicationObject.state");
            }
        }

        private static Exception CreateNotOpenException(CommunicationObject communicationObject)
        {
            return new InvalidOperationException(SR.Format(SRCommon.CommunicationObjectCannotBeUsed, communicationObject.GetType().ToString(), communicationObject.State.ToString()));
        }
    }
}
