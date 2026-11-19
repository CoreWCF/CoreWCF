// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using CoreWCF;
using ServiceContract;

namespace Services
{
    public sealed class UserNameDuplexState
    {
        public int DispatchedCalls;
        public int RejectedCredentials;
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class UserNameDuplexService : IUserNameDuplexService
    {
        private readonly UserNameDuplexState _state;

        public UserNameDuplexService(UserNameDuplexState state)
        {
            _state = state;
        }

        public string Echo(string value)
        {
            Interlocked.Increment(ref _state.DispatchedCalls);
            if (value == "service-fault")
            {
                throw new FaultException<string>("service-fault");
            }

            IUserNameDuplexCallback callback = OperationContext.Current.GetCallbackChannel<IUserNameDuplexCallback>();
            try
            {
                return ServiceSecurityContext.Current.PrimaryIdentity.Name + ":" + callback.Echo(value);
            }
            catch (FaultException<string> error)
            {
                throw new FaultException<string>(error.Detail);
            }
        }
    }
}
