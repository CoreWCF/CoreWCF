// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.IdentityModel.Tokens.Saml2;
using MSIdentityTokens = Microsoft.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Creates SAML2 assertion-based security tokens
    /// </summary>
    public class Saml2SecurityTokenHandler : SecurityTokenHandler
    {
        /// <summary>
        /// The key identifier value type for SAML 2.0 assertion IDs, as defined
        /// by the OASIS Web Services Security SAML Token Profile 1.1. 
        /// </summary>
        public const string TokenProfile11ValueType = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLID";
        private static readonly string[] s_tokenTypeIdentifiers = new string[] { SecurityTokenTypes.Saml2TokenProfile11, SecurityTokenTypes.OasisWssSaml2TokenProfile11 };
        private SecurityTokenSerializer _keyInfoSerializer;
        private readonly MSIdentityTokens.Saml2.Saml2SecurityTokenHandler _internalSaml2SecurityTokenHandler;
        private readonly object _syncObject = new object();
        private readonly SamlSecurityTokenRequirement _samlSecurityTokenRequirement;

        /// <summary>
        /// Creates an instance of <see cref="Saml2SecurityTokenHandler"/>
        /// </summary>
        public Saml2SecurityTokenHandler()
            : this(new SamlSecurityTokenRequirement())
        {
            _internalSaml2SecurityTokenHandler = new MSIdentityTokens.Saml2.Saml2SecurityTokenHandler();
        }

        /// <summary>
        /// Creates an instance of <see cref="Saml2SecurityTokenHandler"/>
        /// </summary>
        /// <param name="samlSecurityTokenRequirement">The SamlSecurityTokenRequirement to be used by the Saml2SecurityTokenHandler instance when validating tokens.</param>
        public Saml2SecurityTokenHandler(SamlSecurityTokenRequirement samlSecurityTokenRequirement)
        {
            _samlSecurityTokenRequirement = samlSecurityTokenRequirement ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(samlSecurityTokenRequirement));
        }

        /// <summary>
        /// Method exposed for extensibility
        /// </summary>
        /// <param name="saml2SecurityTokenHandler"></param>
        public Saml2SecurityTokenHandler(MSIdentityTokens.Saml2.Saml2SecurityTokenHandler saml2SecurityTokenHandler)
            : this(saml2SecurityTokenHandler, new SamlSecurityTokenRequirement())
        {
        }

        public Saml2SecurityTokenHandler(MSIdentityTokens.Saml2.Saml2SecurityTokenHandler saml2SecurityTokenHandler, SamlSecurityTokenRequirement samlSecurityTokenRequirement)
        {
            _internalSaml2SecurityTokenHandler = saml2SecurityTokenHandler;
            _samlSecurityTokenRequirement = samlSecurityTokenRequirement;
        }

        #region TokenValidation
        /// <summary>
        /// Returns value indicates if this handler can validate tokens of type
        /// Saml2SecurityToken.
        /// </summary>
        public override bool CanValidateToken => _internalSaml2SecurityTokenHandler.CanValidateToken;

        /// <summary>
        /// Validates a <see cref="Saml2SecurityToken"/>.
        /// </summary>
        /// <param name="token">The <see cref="Saml2SecurityToken"/> to validate.</param>
        /// <returns>The <see cref="ReadOnlyCollection{T}"/> of <see cref="ClaimsIdentity"/> representing the identities contained in the token.</returns>
        /// <exception cref="ArgumentNullException">The parameter 'token' is null.</exception>
        /// <exception cref="ArgumentException">The token is not assignable from <see cref="Saml2SecurityToken"/>.</exception>
        /// <exception cref="InvalidOperationException">Configuration <see cref="SecurityTokenHandlerConfiguration"/>is null.</exception>
        /// <exception cref="ArgumentException">Saml2SecurityToken.Assertion is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown if Saml2SecurityToken.Assertion.SigningToken is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown if the certificate associated with the token issuer does not pass validation.</exception>
        public override ReadOnlyCollection<ClaimsIdentity> ValidateToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            Saml2SecurityToken samlToken = (Saml2SecurityToken)token;
            string assertionXML = samlToken.AssertionXML;
            SamlTokenValidationParameters tokenValidation = new SamlTokenValidationParameters();
            ClaimsPrincipal claim = _internalSaml2SecurityTokenHandler.ValidateToken(assertionXML,
                tokenValidation.ConvertToTokenValidationParameters(Configuration, samlToken, _samlSecurityTokenRequirement), out MSIdentityTokens.SecurityToken msIdentityToken);
            ClaimsIdentity claimsIdentity = (ClaimsIdentity)claim.Identity;
            if (Configuration.SaveBootstrapContext)
            {
                claimsIdentity.BootstrapContext = new BootstrapContext(token, this);
            }
            List<ClaimsIdentity> identities = new List<ClaimsIdentity>(1)
            {
                claimsIdentity
            };
            return identities.AsReadOnly();
        }
        #endregion

        #region TokenSerialization

        /// <summary>
        /// Indicates whether the current XML element can be read as a token 
        /// of the type handled by this instance.
        /// </summary>
        /// <param name="reader">An XML reader positioned at a start 
        /// element. The reader should not be advanced.</param>
        /// <returns>'True' if the ReadToken method can the element.</returns>
        public override bool CanReadToken(XmlReader reader) => _internalSaml2SecurityTokenHandler.CanReadToken(reader);

        /// <summary>
        /// Deserializes from XML a token of the type handled by this instance.
        /// </summary>
        /// <param name="reader">An XML reader positioned at the token's start 
        /// element.</param>
        /// <returns>An instance of <see cref="Saml2SecurityToken"/>.</returns>
        /// <exception cref="InvalidOperationException">Is thrown if 'Configuration' or 'Configruation.IssuerTokenResolver' is null.</exception>
        public override SecurityToken ReadToken(XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement rstXml = (doc.ReadNode(reader) as XmlElement);
            MSIdentityTokens.Saml2.Saml2SecurityToken internalSecurityToken = _internalSaml2SecurityTokenHandler.ReadSaml2Token(rstXml.OuterXml);
            var securityKeys = ResolveSecurityKeys(internalSecurityToken.Assertion, Configuration.IssuerTokenResolver);
            TryResolveIssuerToken(internalSecurityToken.Assertion, Configuration.IssuerTokenResolver, out SecurityToken token);
            Saml2SecurityToken saml2SecurityToken = new Saml2SecurityToken(internalSecurityToken, securityKeys)
            {
                SigningToken = token,
                AssertionXML = rstXml.OuterXml
            };
            return saml2SecurityToken;
        }

        protected virtual bool TryResolveIssuerToken(Saml2Assertion assertion, SecurityTokenResolver issuerResolver, out SecurityToken token)
        {
            if (null == assertion)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(assertion));
            }
 
            var keyIdentifier = CoreWCF.Security.SecurityUtils.CreateSecurityKeyIdentifier(assertion.Signature.KeyInfo);

            if (keyIdentifier != null
               && issuerResolver != null)
            {
                Fx.Assert(keyIdentifier.Count == 1, "There should only be one key identifier clause");
                return issuerResolver.TryResolveToken(keyIdentifier, out token);
            }
            else
            {
                token = null;
                return false;
            }
        }

        /// <summary>
        /// Resolves the collection of <see cref="SecurityKey"/> referenced in a <see cref="Saml2Assertion"/>.
        /// </summary>
        /// <param name="assertion"><see cref="Saml2Assertion"/> to process.</param>
        /// <param name="resolver"><see cref="SecurityTokenResolver"/> to use in resolving the <see cref="SecurityKey"/>.</param>
        /// <returns>A read only collection of <see cref="SecurityKey"/> contained in the assertion.</returns>
        protected virtual ReadOnlyCollection<SecurityKey> ResolveSecurityKeys(Saml2Assertion assertion, SecurityTokenResolver resolver)
        {
            if (null == assertion)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(assertion));
            }

            // Must have Subject
            Saml2Subject subject = assertion.Subject;
            if (null == subject)
            {
                // No Subject
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4130)));
            }

            // Must have one SubjectConfirmation
            if (0 == subject.SubjectConfirmations.Count)
            {
                // No SubjectConfirmation
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4131)));
            }

            if (subject.SubjectConfirmations.Count > 1)
            {
                // More than one SubjectConfirmation
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4132)));
            }

            // Extract the keys for the given method
            ReadOnlyCollection<SecurityKey> securityKeys;

            Saml2SubjectConfirmation subjectConfirmation = subject.SubjectConfirmations.First();

            // For bearer, ensure there are no keys, set the collection to empty
            // For HolderOfKey, ensure there is at least one key, resolve and create collection
            if (Saml2Constants.ConfirmationMethods.Bearer == subjectConfirmation.Method)
            {
                if (null != subjectConfirmation.SubjectConfirmationData
                    && 0 != subjectConfirmation.SubjectConfirmationData.KeyInfos.Count)
                {
                    // Bearer but has keys
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4133)));
                }

                securityKeys = EmptyReadOnlyCollection<SecurityKey>.Instance;
            }
            else if (Saml2Constants.ConfirmationMethods.HolderOfKey == subjectConfirmation.Method)
            {
                throw new NotSupportedException();
            }
            else
            {
                // SenderVouches, as well as other random things, aren't accepted
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4136, subjectConfirmation.Method)));
            }

            return securityKeys;
        }

        /// <summary>
        /// Gets a boolean indicating if the SecurityTokenHandler can Serialize Tokens. Return true by default.
        /// </summary>
        public override bool CanWriteToken => true;

        /// <summary>
        /// Serializes the given SecurityToken to the XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter into which the token is serialized.</param>
        /// <param name="token">SecurityToken to be serialized.</param>
        /// <exception cref="ArgumentNullException">Input parameter 'writer' or 'token' is null.</exception>
        /// <exception cref="SecurityTokenException">The given 'token' is not a Saml2SecurityToken.</exception>
        public override void WriteToken(XmlWriter writer, SecurityToken token)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            Saml2SecurityToken samlToken = token as Saml2SecurityToken;
            var wrappedSaml2SecurityToken = samlToken.WrappedSaml2SecurityToken;

            if (null != wrappedSaml2SecurityToken)
            {
                _internalSaml2SecurityTokenHandler.WriteToken(writer, wrappedSaml2SecurityToken);
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(token), SR.Format(SR.ID4160));
            }
        }
        #endregion

        /// <summary>
        /// Returns the saml token's token type that is supported by this handler.
        /// </summary>
        public override string[] GetTokenTypeIdentifiers() => s_tokenTypeIdentifiers;

        /// <summary>
        /// Gets or Sets a SecurityTokenSerializers that will be used to serialize and deserializer
        /// SecurtyKeyIdentifier. For example, SamlSubject SecurityKeyIdentifier or Signature 
        /// SecurityKeyIdentifier.
        /// </summary>
        public SecurityTokenSerializer KeyInfoSerializer
        {
            get
            {
                if (_keyInfoSerializer == null)
                {
                    lock (_syncObject)
                    {
                        if (_keyInfoSerializer == null)
                        {
                            SecurityTokenHandlerCollection sthc = ContainingCollection ?? SecurityTokenHandlerCollection.CreateDefaultSecurityTokenHandlerCollection();
                            _keyInfoSerializer = new SecurityTokenSerializerAdapter(sthc);
                        }
                    }
                }

                return _keyInfoSerializer;
            }
            set
            {
                _keyInfoSerializer = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        /// <summary>
        /// Gets the System.Type of the SecurityToken is supported by ththis handler.
        /// </summary>
        public override Type TokenType => typeof(Saml2SecurityToken);
    }
}
