// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Claim = System.Security.Claims.Claim;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// SecurityTokenHandler for X509SecurityToken. By default, the
    /// handler will do chain-trust validation of the Certificate.
    /// </summary>
    public class X509SecurityTokenHandler : SecurityTokenHandler
    {
        private X509CertificateValidator _certificateValidator;
        private readonly X509DataSecurityKeyIdentifierClauseSerializer _x509DataKeyIdentifierClauseSerializer = new X509DataSecurityKeyIdentifierClauseSerializer();

        /// <summary>
        /// Creates an instance of <see cref="X509SecurityTokenHandler"/>. MapToWindows is defaulted to false.
        /// Uses <see cref="X509CertificateValidator.PeerOrChainTrust"/> as the default certificate validator.
        /// </summary>
        public X509SecurityTokenHandler()
            : this(false, null)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="X509SecurityTokenHandler"/> with an X509 certificate validator.
        /// MapToWindows is to false by default.
        /// </summary>
        /// <param name="certificateValidator">The certificate validator.</param>
        public X509SecurityTokenHandler(X509CertificateValidator certificateValidator)
            : this(false, certificateValidator)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="X509SecurityTokenHandler"/>. Uses <see cref="X509CertificateValidator.PeerOrChainTrust"/> 
        /// as the default certificate validator.
        /// </summary>
        /// <param name="mapToWindows">Boolean to indicate if the certificate should be mapped to a 
        /// windows account. Default is false.</param>
        public X509SecurityTokenHandler(bool mapToWindows)
            : this(mapToWindows, null)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="X509SecurityTokenHandler"/>.
        /// </summary>
        /// <param name="mapToWindows">Boolean to indicate if the certificate should be mapped to a windows account.</param>
        /// <param name="certificateValidator">The certificate validator.</param>
        public X509SecurityTokenHandler(bool mapToWindows, X509CertificateValidator certificateValidator)
        {
            MapToWindows = mapToWindows;
            _certificateValidator = certificateValidator;
        }

        /// <summary>
        /// Gets or sets a value indicating whether if the validating token should be mapped to a 
        /// Windows account.
        /// </summary>
        public bool MapToWindows { get; set; }

        /// <summary>
        /// Gets or sets the X509CeritificateValidator that is used by the current instance.
        /// </summary>
        public X509CertificateValidator CertificateValidator
        {
            get
            {
                if (_certificateValidator == null)
                {
                    if (Configuration != null)
                    {
                        return Configuration.CertificateValidator;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return _certificateValidator;
                }
            }

            set
            {
                _certificateValidator = value;
            }
        }

        /// <summary>
        /// Gets or sets the X509NTAuthChainTrustValidator that is used by the current instance during certificate validation when the incoming certificate is mapped to windows.
        /// </summary>
        //public X509NTAuthChainTrustValidator X509NTAuthChainTrustValidator
        //{
        //    get
        //    {
        //        return this.x509NTAuthChainTrustValidator;
        //    }

        //    set
        //    {
        //        this.x509NTAuthChainTrustValidator = value;
        //    }
        //}

        /// <summary>
        /// Gets or sets a value indicating whether XmlDsig defined clause types are 
        /// preferred. Supported XmlDSig defined SecurityKeyIdentifierClause types
        /// are,
        /// 1. X509IssuerSerial
        /// 2. X509SKI
        /// 3. X509Certificate
        /// </summary>
        public bool WriteXmlDSigDefinedClauseTypes { get; set; }

        /// <summary>
        /// Gets a boolean indicating if the handler can validate tokens. 
        /// Returns true by default.
        /// </summary>
        public override bool CanValidateToken
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a boolean indicating if the handler can write tokens.
        /// Returns true by default.
        /// </summary>
        public override bool CanWriteToken
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Checks if the given reader is referring to a &lt;ds:X509Data> element.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader positioned at the SecurityKeyIdentifierClause. </param>
        /// <returns>True if the XmlReader is referring to a &lt;ds:X509Data> element.</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'reader' is null.</exception>
        public override bool CanReadKeyIdentifierClause(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            return _x509DataKeyIdentifierClauseSerializer.CanReadKeyIdentifierClause(reader);
        }

        /// <summary>
        /// Checks if the reader points to a X.509 Security Token as defined in WS-Security.
        /// </summary>
        /// <param name=nameof(reader)>Reader pointing to the token XML.</param>
        /// <returns>Returns true if the element is pointing to a X.509 Security Token.</returns>
        /// <exception cref="ArgumentNullException">The parameter 'reader' is null.</exception>
        public override bool CanReadToken(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            if (reader.IsStartElement(WSSecurity10Constants.Elements.BinarySecurityToken, WSSecurity10Constants.Namespace))
            {
                string valueTypeUri = reader.GetAttribute(WSSecurity10Constants.Attributes.ValueType, null);
                return StringComparer.Ordinal.Equals(valueTypeUri, WSSecurity10Constants.X509TokenType);
            }

            return false;
        }

        /// <summary>
        /// Checks if the given SecurityKeyIdentifierClause can be serialized by this handler. The
        /// supported SecurityKeyIdentifierClause are,
        /// 1. <see cref="System.IdentityModel.Tokens.X509IssuerSerialKeyIdentifierClause"/>
        /// 2. <see cref="System.IdentityModel.Tokens.X509RawDataKeyIdentifierClause"/>
        /// 3. <see cref="System.IdentityModel.Tokens.X509SubjectKeyIdentifierClause"/>
        /// </summary>
        /// <param name=nameof(securityKeyIdentifierClause)>SecurityKeyIdentifierClause to be serialized.</param>
        /// <returns>True if the 'securityKeyIdentifierClause' is supported and if WriteXmlDSigDefinedClausTypes
        /// is set to true.</returns>
        /// <exception cref="ArgumentNullException">The parameter 'securityKeyIdentifierClause' is null.</exception>
        public override bool CanWriteKeyIdentifierClause(SecurityKeyIdentifierClause securityKeyIdentifierClause)
        {
            if (securityKeyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityKeyIdentifierClause));
            }

            return WriteXmlDSigDefinedClauseTypes && _x509DataKeyIdentifierClauseSerializer.CanWriteKeyIdentifierClause(securityKeyIdentifierClause);
        }

        /// <summary>
        /// Gets X509SecurityToken type.
        /// </summary>
        public override Type TokenType
        {
            get { return typeof(X509SecurityToken); }
        }

        /// <summary>
        /// Deserializes a SecurityKeyIdentifierClause referenced by the XmlReader.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader referencing the SecurityKeyIdentifierClause.</param>
        /// <returns>Instance of SecurityKeyIdentifierClause.</returns>
        /// <exception cref="ArgumentNullException">The input parameter 'reader' is null.</exception>
        public override SecurityKeyIdentifierClause ReadKeyIdentifierClause(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            return _x509DataKeyIdentifierClauseSerializer.ReadKeyIdentifierClause(reader);
        }

        /// <summary>
        /// Reads the X.509 Security token referenced by the XmlReader.
        /// </summary>
        /// <param name=nameof(reader)>XmlReader pointing to a X.509 Security token.</param>
        /// <returns>An instance of <see cref="X509SecurityToken"/>.</returns> 
        /// <exception cref="ArgumentNullException">The parameter 'reader' is null.</exception>
        /// <exception cref="XmlException">XmlReader is not pointing to an valid X509SecurityToken as
        /// defined in WS-Security X.509 Token Profile. Or the encodingType specified is other than Base64 
        /// or HexBinary.</exception>
        public override SecurityToken ReadToken(XmlReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            XmlDictionaryReader dicReader = XmlDictionaryReader.CreateDictionaryReader(reader);
            if (!dicReader.IsStartElement(WSSecurity10Constants.Elements.BinarySecurityToken, WSSecurity10Constants.Namespace))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new XmlException(
                        SR.Format(
                        SR.ID4065,
                        WSSecurity10Constants.Elements.BinarySecurityToken,
                        WSSecurity10Constants.Namespace,
                        dicReader.LocalName,
                        dicReader.NamespaceURI)));
            }

            string valueTypeUri = dicReader.GetAttribute(WSSecurity10Constants.Attributes.ValueType, null);

            if (!StringComparer.Ordinal.Equals(valueTypeUri, WSSecurity10Constants.X509TokenType))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new XmlException(
                        SR.Format(
                        SR.ID4066,
                        WSSecurity10Constants.Elements.BinarySecurityToken,
                        WSSecurity10Constants.Namespace,
                        WSSecurity10Constants.Attributes.ValueType,
                        WSSecurity10Constants.X509TokenType,
                        valueTypeUri)));
            }

            string wsuId = dicReader.GetAttribute(WSSecurityUtilityConstants.Attributes.Id, WSSecurityUtilityConstants.Namespace);
            string encoding = dicReader.GetAttribute(WSSecurity10Constants.Attributes.EncodingType, null);

            byte[] binaryData;
            if (encoding == null || StringComparer.Ordinal.Equals(encoding, WSSecurity10Constants.Base64EncodingType))
            {
                binaryData = dicReader.ReadElementContentAsBase64();
            }
            else if (StringComparer.Ordinal.Equals(encoding, WSSecurity10Constants.HexBinaryEncodingType))
            {
                binaryData = SoapHexBinary.Parse(dicReader.ReadElementContentAsString()).Value;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ID4068)));
            }

            return string.IsNullOrEmpty(wsuId) ?
                new X509SecurityToken(new X509Certificate2(binaryData)) :
                new X509SecurityToken(new X509Certificate2(binaryData), wsuId);
        }

        /// <summary>
        /// Gets the X.509 Security Token Type defined in WS-Security X.509 Token profile.
        /// </summary>
        /// <returns>The token type identifier.</returns>
        public override string[] GetTokenTypeIdentifiers()
        {
            return new string[] { SecurityTokenTypes.X509Certificate };
        }

        /// <summary>
        /// Validates an <see cref="X509SecurityToken"/>.
        /// </summary>
        /// <param name="token">The <see cref="X509SecurityToken"/> to validate.</param>
        /// <returns>A <see cref="ReadOnlyCollection{T}"/> of <see cref="ClaimsIdentity"/> representing the identities contained in the token.</returns>
        /// <exception cref="ArgumentNullException">The parameter 'token' is null.</exception>
        /// <exception cref="ArgumentException">The token is not assignable from <see cref="X509SecurityToken"/>.</exception>
        /// <exception cref="InvalidOperationException">Configuration <see cref="SecurityTokenHandlerConfiguration"/>is null.</exception>
        /// <exception cref="SecurityTokenValidationException">The current <see cref="X509CertificateValidator"/> was unable to validate the certificate in the Token.</exception>
        /// <exception cref="InvalidOperationException">Configuration.IssuerNameRegistry is null.</exception>
        /// <exception cref="SecurityTokenException">Configuration.IssuerNameRegistry return null when resolving the issuer of the certificate in the Token.</exception>
        public override ReadOnlyCollection<ClaimsIdentity> ValidateToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if (!(token is X509SecurityToken x509Token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(token), SR.Format(SR.ID0018, typeof(X509SecurityToken)));
            }

            if (Configuration == null)
            {
                throw new InvalidOperationException(SR.Format(SR.ID4274));
            }

            try
            {
                // Validate the token.
                try
                {
                    CertificateValidator.Validate(x509Token.Certificate);
                }
                catch (SecurityTokenValidationException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenValidationException(SR.Format(SR.ID4257,
                        X509Util.GetCertificateId(x509Token.Certificate)), e));
                }

                if (Configuration.IssuerNameRegistry == null)
                {
                    throw new InvalidOperationException(SR.Format(SR.ID4277));
                }

                string issuer = X509Util.GetCertificateIssuerName(x509Token.Certificate, Configuration.IssuerNameRegistry);
                if (string.IsNullOrEmpty(issuer))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.ID4175)));
                }

                ClaimsIdentity identity = null;

                if (!MapToWindows)
                {
                    identity = new ClaimsIdentity("X509");

                    // PARTIAL TRUST: will fail when adding claims, AddClaim is SecurityCritical.
                    identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, AuthenticationMethods.X509));
                }
                else
                {
                    WindowsIdentity windowsIdentity;

                    // if this is the case, then the user has already been mapped to a windows account, just return the identity after adding a couple of claims.
                    if (token is X509WindowsSecurityToken x509WindowsSecurityToken && x509WindowsSecurityToken.WindowsIdentity != null)
                    {
                        // X509WindowsSecurityToken is disposable, make a copy.
                        windowsIdentity = new WindowsIdentity(x509WindowsSecurityToken.WindowsIdentity.Token, x509WindowsSecurityToken.AuthenticationType);
                    }
                    else
                    {
                        throw new NotImplementedException();
                        // Ensure NT_AUTH chain policy for certificate account mapping
                        //if (this.x509NTAuthChainTrustValidator == null)
                        //{
                        //    lock (this.lockObject)
                        //    {
                        //        if (this.x509NTAuthChainTrustValidator == null)
                        //        {
                        //            this.x509NTAuthChainTrustValidator = new X509NTAuthChainTrustValidator();
                        //        }
                        //    }
                        //}

                        //this.x509NTAuthChainTrustValidator.Validate(x509Token.Certificate);
                        //windowsIdentity = ClaimsHelper.CertificateLogon(x509Token.Certificate);
                    }

                    // PARTIAL TRUST: will fail when adding claims, AddClaim is SecurityCritical.
                    windowsIdentity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, AuthenticationMethods.X509));
                    identity = windowsIdentity;
                }

                if (Configuration.SaveBootstrapContext)
                {
                    identity.BootstrapContext = new BootstrapContext(token, this);
                }

                identity.AddClaim(new Claim(ClaimTypes.AuthenticationInstant, XmlConvert.ToString(DateTime.UtcNow, DateTimeFormats.Generated), ClaimValueTypes.DateTime));
                identity.AddClaims(X509Util.GetClaimsFromCertificate(x509Token.Certificate, issuer));

                //this.TraceTokenValidationSuccess(token);
                List<ClaimsIdentity> identities = new List<ClaimsIdentity>(1)
                {
                    identity
                };
                return identities.AsReadOnly();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

               // this.TraceTokenValidationFailure(token, e.Message);
                throw e;
            }
        }

        /// <summary>
        /// Serializes a given SecurityKeyIdentifierClause to the XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter to which the 'securityKeyIdentifierClause' should be serialized.</param>
        /// <param name=nameof(securityKeyIdentifierClause)>SecurityKeyIdentifierClause to serialize.</param>
        /// <exception cref="ArgumentNullException">Input parameter 'wrtier' or 'securityKeyIdentifierClause' is null.</exception>
        /// <exception cref="InvalidOperationException">The property WriteXmlDSigDefinedClauseTypes is false.</exception>
        public override void WriteKeyIdentifierClause(XmlWriter writer, SecurityKeyIdentifierClause securityKeyIdentifierClause)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (securityKeyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityKeyIdentifierClause));
            }

            if (!WriteXmlDSigDefinedClauseTypes)
            {
                throw new InvalidOperationException(SR.Format(SR.ID4261));
            }

            _x509DataKeyIdentifierClauseSerializer.WriteKeyIdentifierClause(writer, securityKeyIdentifierClause);
        }

        /// <summary>
        /// Writes the X509SecurityToken to the given XmlWriter.
        /// </summary>
        /// <param name="writer">XmlWriter to write the token into.</param>
        /// <param name="token">The SecurityToken of type X509SecurityToken to be written.</param>
        /// <exception cref="ArgumentNullException">The parameter 'writer' or 'token' is null.</exception>
        /// <exception cref="ArgumentException">The token is not of type X509SecurityToken.</exception>
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

            if (!(token is X509SecurityToken x509Token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(token), SR.Format(SR.ID0018, typeof(X509SecurityToken)));
            }

            writer.WriteStartElement(WSSecurity10Constants.Elements.BinarySecurityToken, WSSecurity10Constants.Namespace);
            if (!string.IsNullOrEmpty(x509Token.Id))
            {
                writer.WriteAttributeString(WSSecurityUtilityConstants.Attributes.Id, WSSecurityUtilityConstants.Namespace, x509Token.Id);
            }

            writer.WriteAttributeString(WSSecurity10Constants.Attributes.ValueType, null, WSSecurity10Constants.X509TokenType);
            writer.WriteAttributeString(WSSecurity10Constants.Attributes.EncodingType, WSSecurity10Constants.Base64EncodingType);

            byte[] rawData = x509Token.Certificate.GetRawCertData();
            writer.WriteBase64(rawData, 0, rawData.Length);
            writer.WriteEndElement();
        }

        internal static WindowsIdentity KerberosCertificateLogon(X509Certificate2 certificate)
        {
            throw new NotSupportedException();
            //return X509SecurityTokenAuthenticator.KerberosCertificateLogon(certificate);
        }
    }
}
