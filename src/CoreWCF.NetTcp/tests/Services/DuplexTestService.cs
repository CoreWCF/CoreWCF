// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreWCF;
using CoreWCF.IdentityModel.Claims;
using ServiceContract;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class DuplexTestService : IDuplexTestService
    {
        private readonly IProducerConsumerCollection<IDuplexTestCallback> _registeredClients = new ConcurrentBag<IDuplexTestCallback>();

        public bool RegisterDuplexChannel()
        {
            var callback = OperationContext.Current.GetCallbackChannel<IDuplexTestCallback>();
            return _registeredClients.TryAdd(callback);
        }

        public void SendMessage(string message)
        {
            foreach (var client in _registeredClients)
            {
                client.AddMessage(message);
            }
        }

        public bool VerifyWindowsClaimSetUserName(string currentUserName)
        {
            ClaimSet currentUserNameClaimSet = OperationContext.Current.ServiceSecurityContext.AuthorizationContext.ClaimSets[0];
            if (currentUserNameClaimSet.GetType() == typeof(WindowsClaimSet))
            {
                WindowsClaimSet currentUserNameWindowsClaimSet = (WindowsClaimSet)currentUserNameClaimSet;
                string claimUserName;
                if (TryGetClaimValue(currentUserNameWindowsClaimSet, ClaimTypes.Name, out claimUserName))
                {
                    if (StringComparer.Ordinal.Equals(claimUserName, currentUserName))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryGetClaimValue<TClaimResource>(ClaimSet claimSet, string claimType, out TClaimResource resourceValue)
      where TClaimResource : class
        {
            resourceValue = default(TClaimResource);
            IEnumerable<Claim> matchingClaims = claimSet.FindClaims(claimType, Rights.PossessProperty);
            if (matchingClaims == null)
                return false;

            IEnumerator<Claim> enumerator = matchingClaims.GetEnumerator();
            if (enumerator.MoveNext())
            {
                resourceValue = (enumerator.Current.Resource == null) ? null : (enumerator.Current.Resource as TClaimResource);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
