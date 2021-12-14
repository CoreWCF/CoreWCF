// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ServiceSecurityContextTests
    {
        [Fact]
        public void GetIdentitiesWithNoIdentitiesPropertiesFromAuthorizationContextReturnsNull()
        {
            var properties = new Dictionary<string, object>();
            AuthorizationContext authContext = new FakeAuthorizationContext(properties);
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(new IAuthorizationPolicy[] { });

            ServiceSecurityContext context = new ServiceSecurityContext(authContext, authorizationPolicies);
            IList<IIdentity> identities = context.GetIdentities();

            Assert.Null(identities);
        }

        [Fact]
        public void GetIdentitiesReturnsTheIdentitiesPropertyThatWasSuppliedToTheAuthorizationContext()
        {
            var suppliedIdentityList = new List<IIdentity>();
            var properties = new Dictionary<string, object>
            {
                ["Identities"] = suppliedIdentityList
            };
            AuthorizationContext authContext = new FakeAuthorizationContext(properties);
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(new IAuthorizationPolicy[] { });

            ServiceSecurityContext context = new ServiceSecurityContext(authContext, authorizationPolicies);
            IList<IIdentity> identities = context.GetIdentities();

            Assert.Same(suppliedIdentityList, identities);
        }

        private class FakeAuthorizationContext : AuthorizationContext
        {
            public FakeAuthorizationContext(IDictionary<string, object> properties)
            {
                Properties = properties;
            }

            public override string Id => throw new NotImplementedException();

            public override ReadOnlyCollection<ClaimSet> ClaimSets => throw new NotImplementedException();

            public override DateTime ExpirationTime => throw new NotImplementedException();

            public override IDictionary<string, object> Properties { get; }
        }
    }
}
