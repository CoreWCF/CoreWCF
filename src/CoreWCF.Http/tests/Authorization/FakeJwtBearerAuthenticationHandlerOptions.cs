// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;

namespace CoreWCF.Http.Tests.Authorization
{
    internal class FakeJwtBearerAuthenticationHandlerOptions : AuthenticationSchemeOptions
    {
        public List<string> ScopeClaimValues { get; set; } = new();
        public bool IsAuthenticated { get; set; }
    }
}
