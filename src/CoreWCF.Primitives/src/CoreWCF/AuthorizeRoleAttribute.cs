// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Claims;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.OperationContract, Inherited = true)]
    public class AuthorizeRoleAttribute : Attribute, IAuthorizeOperation
    {
        public string AllowedRoles { get; set; }

        public void BuildClaim(OperationDescription operationDescription, DispatchOperation dispatchOperation)
        {
            if (operationDescription == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operationDescription));
            }

            if (dispatchOperation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatchOperation));
            }
            if (String.IsNullOrEmpty(AllowedRoles))
            {
                return;
            }
            string[] roles = AllowedRoles.Split(',');
            List<Claim> allClaims = new List<Claim>();
            foreach(string role in roles)
            {
                allClaims.Add(new Claim(ClaimTypes.Role, role.Trim(), Rights.Identity));
            }
            if(allClaims.Count > 0)
            {
                dispatchOperation.AuthorizeClaims.TryAdd(nameof(AuthorizeRoleAttribute), allClaims);
            }

        }
    }
}
