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

namespace CoreWCF.WebHttp.Tests.Authorization
{
    internal class FakeJwtBearerAuthenticationHandler : AuthenticationHandler<FakeJwtBearerAuthenticationHandlerOptions>
    {
        public const string AuthenticationScheme = "FakeJwtBearer";

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

            foreach (string scopeClaimValue in _options.CurrentValue.ScopeClaimValues)
            {
                claims.Add(new Claim("scope", scopeClaimValue));
            }

            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }
    }
}
