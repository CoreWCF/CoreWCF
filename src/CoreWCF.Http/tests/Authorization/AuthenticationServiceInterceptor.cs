// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Http.Tests.Authorization;


internal class AuthenticationServiceHolder
{
    public bool IsAuthenticateAsyncCalled { get; set; }
    public bool IsChallengeAsyncCalled { get; set; }
}

internal class AuthenticationServiceInterceptor : IAuthenticationService
{
    private readonly IAuthenticationService _authenticationService;
    private readonly AuthenticationServiceHolder _holder;

    public AuthenticationServiceInterceptor(AuthenticationService authenticationService, AuthenticationServiceHolder holder)
    {
        _authenticationService = authenticationService;
        _holder = holder;
    }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
    {
        _holder.IsAuthenticateAsyncCalled = true;
        return _authenticationService.AuthenticateAsync(context, scheme);
    }

    public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties)
    {
        _holder.IsChallengeAsyncCalled = true;
        return _authenticationService.ChallengeAsync(context, scheme, properties);
    }
        

    public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties)
        => _authenticationService.ForbidAsync(context, scheme, properties);

    public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal,
        AuthenticationProperties properties)
        => _authenticationService.SignInAsync(context, scheme, principal, properties);

    public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties)
        => _authenticationService.SignOutAsync(context, scheme, properties);
}
