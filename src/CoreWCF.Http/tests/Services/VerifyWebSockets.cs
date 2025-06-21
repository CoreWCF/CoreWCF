// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, AddressFilterMode = AddressFilterMode.Any)]
    public class VerifyWebSockets : ServiceContract.IVerifyWebSockets
    {
        private bool successMessage;

        // This operation cannot return anything so in order to validate that it was called set the InstanceContextMode
        //  to PerSession and set a variable that we can get with a call to the ValidateWebSocketsUsed operation.
        public void ForceWebSocketsUse()
        {
            successMessage = true;
        }

        public bool ValidateWebSocketsUsed()
        {
            return successMessage;
        }
    }
}
