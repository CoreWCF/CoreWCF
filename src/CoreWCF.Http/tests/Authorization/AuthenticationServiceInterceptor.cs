// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Http.Tests.Authorization;

internal class AuthenticationServiceInterceptor : IAuthenticationService
{
    private readonly IAuthenticationService _authenticationService;

    public bool IsAuthenticateAsyncCalled { get; private set; }

    public AuthenticationServiceInterceptor(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;

        HttpClient a;
        
    }

    public async Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
    {
        var result = await _authenticationService.AuthenticateAsync(context, scheme);
        IsAuthenticateAsyncCalled = true;
        return result;
    }

    public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties)
        => _authenticationService.ChallengeAsync(context, scheme, properties);

    public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties)
        => _authenticationService.ForbidAsync(context, scheme, properties);

    public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal,
        AuthenticationProperties properties)
        => _authenticationService.SignInAsync(context, scheme, principal, properties);

    public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties)
        => _authenticationService.SignOutAsync(context, scheme, properties);
}
