
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Configuration
{
    /// <summary>
    /// Defines caches supported by IdentityModel for TokenReplay and SecuritySessionTokens
    /// </summary>
    public sealed class IdentityModelCaches
    {
        private TokenReplayCache _tokenReplayCache = new DefaultTokenReplayCache();
        private SessionSecurityTokenCache _sessionSecurityTokenCache = new MruSessionSecurityTokenCache();

        /// <summary>
        /// Gets or sets the TokenReplayCache that is used to determine replayed token.
        /// </summary>
        public TokenReplayCache TokenReplayCache
        {
            get
            {
                return _tokenReplayCache;
            }

            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _tokenReplayCache = value;
            }
        }

        /// <summary>
        /// Gets or sets the SessionSecurityTokenCache that is used to cache the <see cref="SessionSecurityToken"/>
        /// </summary>
        public SessionSecurityTokenCache SessionSecurityTokenCache
        {
            get
            {
                return _sessionSecurityTokenCache;
            }

            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _sessionSecurityTokenCache = value;
            }
        }

    }
}
