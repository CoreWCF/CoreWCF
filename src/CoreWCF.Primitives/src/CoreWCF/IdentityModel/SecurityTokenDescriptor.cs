// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Xml;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Protocols.WSTrust;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// This is a place holder for all the attributes related to the issued token.
    /// </summary>
    public class SecurityTokenDescriptor
    {
        private string _appliesToAddress;

        /// <summary>
        /// Gets or sets the address for the <see cref="RequestSecurityTokenResponse"/> AppliesTo property.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not an absolute URI.</exception>
        public string AppliesToAddress
        {
            get 
            { 
                return _appliesToAddress; 
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    //if (!UriUtil.CanCreateValidUri(value, UriKind.Absolute))
                    //{
                    //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID2002)));
                    //}
                }

                _appliesToAddress = value;
            }
        }

        /// <summary>
        /// Gets or sets the address for the <see cref="RequestSecurityTokenResponse"/> ReplyToAddress property.
        /// </summary>
        public string ReplyToAddress { get; set; }

        /// <summary>
        /// Gets or sets the credentials used to encrypt the token.
        /// </summary>
        public EncryptingCredentials EncryptingCredentials { get; set; }

        /// <summary>
        /// Gets or sets the credentials used to sign the token.
        /// </summary>
        public SigningCredentials SigningCredentials { get; set; }

        /// <summary>
        /// Gets or sets the SecurityKeyIdentifierClause when the token is attached 
        /// to the message.
        /// </summary>
        public SecurityKeyIdentifierClause AttachedReference { get; set; }

        /// <summary>
        /// Gets or sets the issuer name, which may be used inside the issued token as well.
        /// </summary>
        public string TokenIssuerName { get; set; }

        /// <summary>
        /// Gets or sets the proof descriptor, which can be used to modify some fields inside
        /// the RSTR, such as requested proof token.
        /// </summary>
        //public ProofDescriptor Proof
        //{
        //    get { return this.proofDescriptor; }
        //    set { this.proofDescriptor = value; }
        //}

        /// <summary>
        /// Gets the properties bag to extend the object.
        /// </summary>
        public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the issued security token.
        /// </summary>
        public SecurityToken Token { get; set; }

        /// <summary>
        /// Gets or sets the token type of the issued token.
        /// </summary>
        public string TokenType { get; set; }

        /// <summary>
        /// Gets or sets the unattached token reference to refer to the issued token when it is not 
        /// attached to the message.
        /// </summary>
        public SecurityKeyIdentifierClause UnattachedReference { get; set; }

        /// <summary>
        /// Gets or sets the lifetime information for the issued token.
        /// </summary>
        public Lifetime Lifetime { get; set; }

        /// <summary>
        /// Gets or sets the OutputClaims to be included in the issued token.
        /// </summary>
        public ClaimsIdentity Subject { get; set; }

        /// <summary>
        /// Gets or sets the AuthenticationInformation.
        /// </summary>
        public AuthenticationInformation AuthenticationInfo { get; set; }

        /// <summary>
        /// Adds a <see cref="Claim"/> for the authentication type to the claim collection of 
        /// the <see cref="SecurityTokenDescriptor"/>
        /// </summary>
        /// <param name="authType">The authentication type.</param>
        public void AddAuthenticationClaims(string authType)
        {
            AddAuthenticationClaims(authType, DateTime.UtcNow);
        }

        /// <summary>
        /// Adds <see cref="Claim"/>s for the authentication type and the authentication instant 
        /// to the claim collection of the <see cref="SecurityTokenDescriptor"/>
        /// </summary>
        /// <param name="authType">Specifies the authentication type</param>
        /// <param name="time">Specifies the authentication instant in UTC. If the input is not in UTC, it is converted to UTC.</param> 
        public void AddAuthenticationClaims(string authType, DateTime time)
        {
            Subject.AddClaim(
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.AuthenticationMethod, authType, ClaimValueTypes.String));

            Subject.AddClaim(
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.AuthenticationInstant, XmlConvert.ToString(time.ToUniversalTime(), DateTimeFormats.Generated), ClaimValueTypes.DateTime));
        }
    }
}
