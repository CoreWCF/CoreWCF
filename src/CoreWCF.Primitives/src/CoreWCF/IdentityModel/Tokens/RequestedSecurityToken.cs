// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Tokens;
using System.Xml;

namespace CoreWCF.IdentityModel.Protocols.WSTrust
{
    /// <summary>
    /// This class defines the requested security token which is usually opaque to 
    /// the token requestor.
    /// </summary>
    public class RequestedSecurityToken
    {
        private XmlElement _tokenAsXml;
        private SecurityToken _requestedToken;

        /// <summary>
        /// Creates an instance of RequestedSecurityToken using the issued token. This is usually used 
        /// on the token issuer end.
        /// </summary>
        /// <param name="token">The Security token requested.</param>
        public RequestedSecurityToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            _requestedToken = token;
        }

        /// <summary>
        /// Creates an instance of RequestedSecurityToken using the token xml. This is usually used on the 
        /// token receiving end.
        /// </summary>
        /// <param name="tokenAsXml">XML representation of the token.</param>
        public RequestedSecurityToken(XmlElement tokenAsXml)
        {
            if (tokenAsXml == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenAsXml));
            }

            _tokenAsXml = tokenAsXml;
        }

        /// <summary>
        /// Returns the XML representation of the token when the RequestedSecurityToken was constructed 
        /// using the token xml. This property getter could return null if the RequestedSecurityToken was constructed
        /// using a security token.
        /// </summary>
        public virtual XmlElement SecurityTokenXml
        {
            get
            {
                return _tokenAsXml;
            }
        }

        /// <summary>
        /// Gets the issued security token when the RequestedSecurityToken was constructed using the token 
        /// itself. This property getter could return null if the RequestedSecurityToken was constructed using the 
        /// token xml. 
        /// </summary>
        public SecurityToken SecurityToken
        {
            get
            {
                return _requestedToken;
            }
        }
    }
}
