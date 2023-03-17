// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace CoreWCF.Http.Tests.Authorization;

internal class AuthorizationServiceHolder
{
    public bool IsAuthorizeAsyncCalled { get; set; }
}

internal class AuthorizationServiceInterceptor : IAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AuthorizationServiceHolder _holder;

    public AuthorizationServiceInterceptor(DefaultAuthorizationService authorizationService, AuthorizationServiceHolder holder)
    {
        _authorizationService = authorizationService;
        _holder = holder;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        _holder.IsAuthorizeAsyncCalled = true;
        var result = await _authorizationService.AuthorizeAsync(user, resource, requirements);
        return result;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
    {
        _holder.IsAuthorizeAsyncCalled = true;
        var result = await _authorizationService.AuthorizeAsync(user, resource, policyName);
        return result;
    }
}
