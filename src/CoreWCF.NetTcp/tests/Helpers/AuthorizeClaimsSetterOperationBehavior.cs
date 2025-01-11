// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Claims;
using System.Collections.Generic;

namespace Helpers
{
    internal class AuthorizeClaimsSetterOperationBehavior : IOperationBehavior
    {
        private Dictionary<string, List<Claim>> _authorizeClaims;
        private bool _clearExisting;

        public AuthorizeClaimsSetterOperationBehavior(Dictionary<string, List<Claim>> authorizeClaims, bool clearExisting)
        {
            _authorizeClaims = authorizeClaims;
            _clearExisting = clearExisting;
        }

        public void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation) { }


        public void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation)
        {
            if (_clearExisting)
            {
                dispatchOperation.AuthorizeClaims.Clear();
            }

            foreach (var claims in _authorizeClaims)
            {
                dispatchOperation.AuthorizeClaims.TryAdd(claims.Key, claims.Value);
            }
        }

        public void Validate(OperationDescription operationDescription) { }

    }
}