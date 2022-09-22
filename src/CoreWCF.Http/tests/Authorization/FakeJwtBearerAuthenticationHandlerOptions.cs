// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;

namespace CoreWCF.Http.Tests.Authorization
{
    public class FakeJwtBearerAuthenticationHandlerOptions : AuthenticationSchemeOptions
    {
        public string DefaultScopeClaimValue { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
    }
}
