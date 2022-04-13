// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;
using Microsoft.IdentityModel.Tokens.Saml;
using MSIdentityTokens = Microsoft.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// This class implements a SecurityTokenHandler for a Saml11 token.  It contains functionality for: Creating, Serializing and Validating 
    /// a Saml 11 Token.
    /// </summary>
    public class SamlSecurityTokenHandler : SecurityTokenHandler
    {
        private static readonly string[] s_tokenTypeIdentifiers = new string[] { SecurityTokenTypes.SamlTokenProfile11, SecurityTokenTypes.OasisWssSamlTokenProfile11 };
        private SecurityTokenSerializer _keyInfoSerializer;
        private readonly MSIdentityTokens.Saml.SamlSecurityTokenHandler _internalSamlSecurityTokenHandler;
        private readonly object _syncObject = new object();
        private readonly SamlSecurityTokenRequirement _samlSecurityTokenRequirement;

        /// <summary>
        /// Initializes an instance of <see cref="SamlSecurityTokenHandler"/>
        /// </summary>
        public SamlSecurityTokenHandler()
            : this(new SamlSecurityTokenRequirement())
        {
            _internalSamlSecurityTokenHandler = new MSIdentityTokens.Saml.SamlSecurityTokenHandler();
        }

        /// <summary>
        /// Initializes an instance of <see cref="SamlSecurityTokenHandler"/>
        /// </summary>
        /// <param name="samlSecurityTokenRequirement">The SamlSecurityTokenRequirement to be used by the Saml11SecurityTokenHandler instance when validating tokens.</param>
        public SamlSecurityTokenHandler(SamlSecurityTokenRequirement samlSecurityTokenRequirement)
        {
            _samlSecurityTokenRequirement = samlSecurityTokenRequirement ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(samlSecurityTokenRequirement));
        }

        /// <summary>
        /// Method exposed for extensibility
        /// </summary>
        /// <param name="saml2SecurityTokenHandler"></param>
        public SamlSecurityTokenHandler(MSIdentityTokens.Saml.SamlSecurityTokenHandler samlSecurityTokenHandler)
            : this(samlSecurityTokenHandler, new SamlSecurityTokenRequirement())
        {
        }

        public SamlSecurityTokenHandler(MSIdentityTokens.Saml.SamlSecurityTokenHandler samlSecurityTokenHandler, SamlSecurityTokenRequirement samlSecurityTokenRequirement)
        {
            _internalSamlSecurityTokenHandler = samlSecurityTokenHandler;
            _samlSecurityTokenRequirement = samlSecurityTokenRequirement;
        }

        #region TokenValidation
        /// <summary>
        /// Returns value indicates if this handler can validate tokens of type
        /// SamlSecurityToken.
        /// </summary>
        public override bool CanValidateToken => _internalSamlSecurityTokenHandler.CanValidateToken;

        /// <summary>
        /// Validates a <see cref="SamlSecurityToken"/>.
        /// </summary>
        /// <param name="token">The <see cref="SamlSecurityToken"/> to validate.</param>
        /// <returns>The <see cref="ReadOnlyCollection{T}"/> of <see cref="ClaimsIdentity"/> representing the identities contained in the token.</returns>
        /// <exception cref="ArgumentNullException">The parameter 'token' is null.</exception>
        /// <exception cref="ArgumentException">The token is not assignable from <see cref="SamlSecurityToken"/>.</exception>
        /// <exception cref="InvalidOperationException">Configuration <see cref="SecurityTokenHandlerConfiguration"/>is null.</exception>
        /// <exception cref="ArgumentException">SamlSecurityToken.Assertion is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown if SamlSecurityToken.Assertion.SigningToken is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown if the certificate associated with the token issuer does not pass validation.</exception>
        public override ReadOnlyCollection<ClaimsIdentity> ValidateToken(SecurityToken token)
        {
            SamlSecurityToken samlToken = (SamlSecurityToken)token;
            string assertionXML = samlToken.AssertionXML;
            SamlTokenValidationParameters tokenValidation = new SamlTokenValidationParameters();
            ClaimsPrincipal claim = _internalSamlSecurityTokenHandler.ValidateToken(assertionXML,
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
        public override bool CanReadToken(XmlReader reader) => _internalSamlSecurityTokenHandler.CanReadToken(reader);

        /// <summary>
        /// Deserializes from XML a token of the type handled by this instance.
        /// </summary>
        /// <param name="reader">An XML reader positioned at the token's start 
        /// element.</param>
        /// <returns>An instance of <see cref="SamlSecurityToken"/>.</returns>
        /// <exception cref="InvalidOperationException">Is thrown if 'Configuration' or 'Configruation.IssuerTokenResolver' is null.</exception>
        public override SecurityToken ReadToken(XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement rstXml = (doc.ReadNode(reader) as XmlElement);
            MSIdentityTokens.Saml.SamlSecurityToken internalSecurityToken = _internalSamlSecurityTokenHandler.ReadSamlToken(rstXml.OuterXml);
            TryResolveIssuerToken(internalSecurityToken.Assertion, Configuration.IssuerTokenResolver, out SecurityToken token);
            SamlSecurityToken samlSecurityToken = new SamlSecurityToken(internalSecurityToken, BuildCryptoList(internalSecurityToken.Assertion))
            {
                SigningToken = token,
                AssertionXML = rstXml.OuterXml
            };
            return samlSecurityToken;
        }

        protected virtual bool TryResolveIssuerToken(SamlAssertion assertion, SecurityTokenResolver issuerResolver, out SecurityToken token)
        {
            if (null == assertion)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(assertion));
            }

            SecurityKeyIdentifier keyIdentifier = CoreWCF.Security.SecurityUtils.CreateSecurityKeyIdentifier(assertion.Signature.KeyInfo);

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
        /// Gets a boolean indicating if the SecurityTokenHandler can Serialize Tokens. Return true by default.
        /// </summary>
        public override bool CanWriteToken => true;

        /// <summary>
        /// Serializes the given SecurityToken to the XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter into which the token is serialized.</param>
        /// <param name="token">SecurityToken to be serialized.</param>
        /// <exception cref="ArgumentNullException">Input parameter 'writer' or 'token' is null.</exception>
        /// <exception cref="SecurityTokenException">The given 'token' is not a SamlSecurityToken.</exception>
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

            SamlSecurityToken samlToken = token as SamlSecurityToken;
            var wrappedSamlSecurityToken = samlToken.WrappedSamlSecurityToken;

            if (null != wrappedSamlSecurityToken)
            {
                _internalSamlSecurityTokenHandler.WriteToken(writer, wrappedSamlSecurityToken);
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
        public override Type TokenType => typeof(SamlSecurityToken);


        ////https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Token/SamlAssertion.cs,404
        //Below is the the way SecurityKey is populated. The only difference is we extract crypto at the same time while iterating
        //over SamlSubject whereas in WCF it's populated from reader.
        private ReadOnlyCollection<SecurityKey> BuildCryptoList(SamlAssertion assertion)
        {
            List<SecurityKey> cryptoList = new List<SecurityKey>();

            for (int i = 0; i < assertion.Statements.Count; ++i)
            {
                SamlSubjectStatement statement = assertion.Statements[i] as SamlSubjectStatement;
                if (statement != null && statement.Subject !=null && statement.Subject.KeyInfo !=null)
                {
                    bool skipCrypto = false;

                    //This code is simplified version of
                    //https://github.com/microsoft/referencesource/blob/4e6dea7a9c7cbb4e6b000b05a099e7168d1b6960/System.IdentityModel/System/IdentityModel/Token/SamlSecurityTokenHandler.cs,2361
                    SecurityKeyIdentifier keyIdentifier = CoreWCF.Security.SecurityUtils.CreateSecurityKeyIdentifier(statement.Subject.KeyInfo);
                    //subject.KeyIdentifier = ReadSubjectKeyInfo(reader);
                    SecurityKey crypto = ResolveSubjectKeyIdentifier(keyIdentifier);
                    if(crypto == null)
                    {
                        crypto = new SecurityKeyElement(keyIdentifier, Configuration.ServiceTokenResolver);
                    }
                    //end of crypto population

                    InMemorySymmetricSecurityKey inMemorySymmetricSecurityKey = crypto as InMemorySymmetricSecurityKey;
                    if (inMemorySymmetricSecurityKey != null)
                    {

                        // Verify that you have not already added this to crypto list.
                        for (int j = 0; j < cryptoList.Count; ++j)
                        {
                            if ((cryptoList[j] is InMemorySymmetricSecurityKey) && (cryptoList[j].KeySize == inMemorySymmetricSecurityKey.KeySize))
                            {
                                byte[] key1 = ((InMemorySymmetricSecurityKey)cryptoList[j]).GetSymmetricKey();
                                byte[] key2 = inMemorySymmetricSecurityKey.GetSymmetricKey();
                                int k = 0;
                                for (k = 0; k < key1.Length; ++k)
                                {
                                    if (key1[k] != key2[k])
                                    {
                                        break;
                                    }
                                }
                                skipCrypto = (k == key1.Length);
                            }

                            if (skipCrypto)
                                break;
                        }
                    }
                    if (!skipCrypto && (crypto != null))
                    {
                        cryptoList.Add(crypto);
                    }
                }
            }

            return cryptoList.AsReadOnly();

        }

        /// <summary>
        /// Resolves the SecurityKeyIdentifier specified in a saml:Subject element. 
        /// </summary>
        /// <param name="subjectKeyIdentifier">SecurityKeyIdentifier to resolve into a key.</param>
        /// <returns>SecurityKey</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'subjectKeyIdentifier' is null.</exception>
        protected virtual SecurityKey ResolveSubjectKeyIdentifier(SecurityKeyIdentifier subjectKeyIdentifier)
        {
            if (subjectKeyIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(subjectKeyIdentifier));
            }

            if (Configuration == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4274));
            }

            if (Configuration.ServiceTokenResolver == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4276));
            }

            SecurityKey key = null;
            foreach (SecurityKeyIdentifierClause clause in subjectKeyIdentifier)
            {
                if (Configuration.ServiceTokenResolver.TryResolveSecurityKey(clause, out key))
                {
                    return key;
                }
            }

            if (subjectKeyIdentifier.CanCreateKey)
            {
                return subjectKeyIdentifier.CreateKey();
            }

            return null;
        }

    }
}
