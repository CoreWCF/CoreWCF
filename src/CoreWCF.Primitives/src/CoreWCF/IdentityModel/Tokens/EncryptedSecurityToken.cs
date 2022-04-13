// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// A pseudo-token which handles encryption for a token which
    /// does not natively support it.
    /// </summary>
    /// <remarks>
    /// For example, a SamlSecurityToken has no notion of how to encrypt
    /// itself, so to issue an encrypted SAML11 assertion, wrap a 
    /// SamlSecurityToken with an EncryptedSecurityToken and provide 
    /// appropriate EncryptingCredentials.
    /// </remarks>
    public class EncryptedSecurityToken : SecurityToken
    {
        /// <summary>
        /// Creates an instance of EncryptedSecurityToken.
        /// </summary>
        /// <param name="token">The <see cref="SecurityToken"/> to encrypt.</param>
        /// <param name="encryptingCredentials">The <see cref="EncryptingCredentials"/> to use for encryption.</param>
        public EncryptedSecurityToken(SecurityToken token, EncryptingCredentials encryptingCredentials)
        {
            EncryptingCredentials = encryptingCredentials ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encryptingCredentials));
            Token = token ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
        }

        /// <summary>
        /// Inherited from <see cref="SecurityToken"/>.
        /// </summary>
        public override bool CanCreateKeyIdentifierClause<T>()
        {
            return Token.CanCreateKeyIdentifierClause<T>();
        }

        /// <summary>
        /// Inherited from <see cref="SecurityToken"/>.
        /// </summary>
        public override T CreateKeyIdentifierClause<T>()
        {
            return Token.CreateKeyIdentifierClause<T>();
        }

        /// <summary>
        /// Gets the <see cref="EncryptingCredentials"/> to use for encryption.
        /// </summary>
        public EncryptingCredentials EncryptingCredentials { get; }

        /// <summary>
        /// Gets a unique identifier of the security token.
        /// </summary>
        public override string Id => Token.Id;

        /// <summary>
        /// Inherited from <see cref="SecurityToken"/>.
        /// </summary>
        public override bool MatchesKeyIdentifierClause(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            return Token.MatchesKeyIdentifierClause(keyIdentifierClause);
        }

        /// <summary>
        /// Inherited from <see cref="SecurityToken"/>.
        /// </summary>
        public override SecurityKey ResolveKeyIdentifierClause(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            return Token.ResolveKeyIdentifierClause(keyIdentifierClause);
        }

        /// <summary>
        /// Inherited from <see cref="SecurityToken"/>.
        /// </summary>
        public override ReadOnlyCollection<SecurityKey> SecurityKeys => Token.SecurityKeys;

        /// <summary>
        /// Gets the encrypted <see cref="SecurityToken"/>.
        /// </summary>
        public SecurityToken Token { get; }

        /// <summary>
        /// Gets the first instant in time at which this security token is valid.
        /// </summary>
        public override DateTime ValidFrom => Token.ValidFrom;

        /// <summary>
        /// Gets the last instant in time at which this security token is valid.
        /// </summary>
        public override DateTime ValidTo => Token.ValidTo;
    }
}
