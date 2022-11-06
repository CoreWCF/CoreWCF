// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace CoreWCF.Http.Tests.Authorization;

internal class AuthorizationServiceInterceptor : IAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;

    public bool IsAuthorizeAsyncCalled { get; private set; }

    public AuthorizationServiceInterceptor(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        var result = await _authorizationService.AuthorizeAsync(user, resource, requirements);
        IsAuthorizeAsyncCalled = true;
        return result;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
    {
        var result = await _authorizationService.AuthorizeAsync(user, resource, policyName);
        IsAuthorizeAsyncCalled = true;
        return result;
    }
}
