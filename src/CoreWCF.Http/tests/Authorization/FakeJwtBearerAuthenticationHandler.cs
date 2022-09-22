// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Http.Tests.Authorization
{
    public class FakeJwtBearerAuthenticationHandler : AuthenticationHandler<FakeJwtBearerAuthenticationHandlerOptions>
    {
        private readonly IOptionsMonitor<FakeJwtBearerAuthenticationHandlerOptions> _options;

        public FakeJwtBearerAuthenticationHandler(
            IOptionsMonitor<FakeJwtBearerAuthenticationHandlerOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
            _options = options;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!_options.CurrentValue.IsAuthenticated)
            {
                return Task.FromResult(AuthenticateResult.Fail(string.Empty));
            }

            List<Claim> claims = new(new[] { new Claim("sub", Guid.NewGuid().ToString()) });

            if (!string.IsNullOrWhiteSpace(_options.CurrentValue.DefaultScopeClaimValue))
            {
                claims.Add(new Claim("scope", _options.CurrentValue.DefaultScopeClaimValue));
            }

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Bearer");

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }
    }
}
