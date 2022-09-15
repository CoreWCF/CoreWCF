// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Configuration
{
    public sealed class IssuedTokenParametersElement : ServiceModelConfigurationElement
    {
        private Collection<IssuedTokenParametersElement> _optionalIssuedTokenParameters = null;

        [ConfigurationProperty(ConfigurationStrings.DefaultMessageSecurityVersion)]
        [TypeConverter(typeof(MessageSecurityVersionConverter))]
        public MessageSecurityVersion DefaultMessageSecurityVersion
        {
            get { return (MessageSecurityVersion)base[ConfigurationStrings.DefaultMessageSecurityVersion]; }
            set { base[ConfigurationStrings.DefaultMessageSecurityVersion] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.AdditionalRequestParameters)]
        public XmlElementElementCollection AdditionalRequestParameters
        {
            get { return (XmlElementElementCollection)base[ConfigurationStrings.AdditionalRequestParameters]; }
        }

        [ConfigurationProperty(ConfigurationStrings.ClaimTypeRequirements)]
        public ClaimTypeElementCollection ClaimTypeRequirements
        {
            get { return (ClaimTypeElementCollection)base[ConfigurationStrings.ClaimTypeRequirements]; }
        }

        [ConfigurationProperty(ConfigurationStrings.Issuer)]
        public IssuedTokenParametersEndpointAddressElement Issuer
        {
            get { return (IssuedTokenParametersEndpointAddressElement)base[ConfigurationStrings.Issuer]; }
        }

        [ConfigurationProperty(ConfigurationStrings.IssuerMetadata)]
        public EndpointAddressElementBase IssuerMetadata
        {
            get { return (EndpointAddressElementBase)base[ConfigurationStrings.IssuerMetadata]; }
        }

        [ConfigurationProperty(ConfigurationStrings.KeySize, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int KeySize
        {
            get { return (int)base[ConfigurationStrings.KeySize]; }
            set { base[ConfigurationStrings.KeySize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.KeyType, DefaultValue = SecurityBindingDefaults.DefaultKeyType)]
        public SecurityKeyType KeyType
        {
            get { return (SecurityKeyType)base[ConfigurationStrings.KeyType]; }
            set { base[ConfigurationStrings.KeyType] = value; }
        }

        internal Collection<IssuedTokenParametersElement> OptionalIssuedTokenParameters
        {
            get
            {
                // OptionalIssuedTokenParameters built on assumption that configuration is writable.
                // This should be protected at the callers site.  If assumption is invalid, then
                // configuration system is in an indeterminate state.  Need to stop in a manner that
                // user code can not capture.
                if (IsReadOnly())
                {
                    Fx.Assert("IssuedTokenParametersElement.OptionalIssuedTokenParameters should only be called by Admin APIs");
                    DiagnosticUtility.FailFast("IssuedTokenParametersElement.OptionalIssuedTokenParameters should only be called by Admin APIs");
                }

                // No need to worry about a race condition here-- this method is not meant to be called by multi-threaded
                // apps. It is only supposed to be called by svcutil and single threaded equivalents.
                if (_optionalIssuedTokenParameters == null)
                {
                    _optionalIssuedTokenParameters = new Collection<IssuedTokenParametersElement>();
                }
                return _optionalIssuedTokenParameters;
            }
        }


        [ConfigurationProperty(ConfigurationStrings.TokenType, DefaultValue = "")]
        [StringValidator(MinLength = 0)]
        public string TokenType
        {
            get { return (string)base[ConfigurationStrings.TokenType]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }

                base[ConfigurationStrings.TokenType] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.UseStrTransform, DefaultValue = false)]
        public bool UseStrTransform
        {
            get { return (bool)base[ConfigurationStrings.UseStrTransform]; }
            set { base[ConfigurationStrings.UseStrTransform] = value; }
        }

        internal void ApplyConfiguration(IssuedSecurityTokenParameters parameters)
        {
            if (parameters == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)));

            if (AdditionalRequestParameters != null)
            {
                foreach (XmlElementElement e in AdditionalRequestParameters)
                {
                    parameters.AdditionalRequestParameters.Add(e.XmlElement);
                }
            }

            if (ClaimTypeRequirements != null)
            {
                foreach (ClaimTypeElement c in ClaimTypeRequirements)
                {
                    parameters.ClaimTypeRequirements.Add(new ClaimTypeRequirement(c.ClaimType, c.IsOptional));
                }
            }

            parameters.KeySize = KeySize;
            parameters.KeyType = this.KeyType;
            parameters.DefaultMessageSecurityVersion = DefaultMessageSecurityVersion;
            parameters.UseStrTransform = UseStrTransform;

            if (!string.IsNullOrEmpty(TokenType))
            {
                parameters.TokenType = TokenType;
            }
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.Issuer].ValueOrigin)
            {
                //TODO: ? I am not sure of the best way of loading the Issuer EndPoint and Binding
                
                throw new PlatformNotSupportedException(nameof(Issuer));
                //TODO: Implement IssuedTokenParameters
                //this.Issuer.Validate();

                //TODO: Implement IssuedTokenParameters
                //parameters.IssuerAddress = ConfigLoader.LoadEndpointAddress(this.Issuer);

                // if (!string.IsNullOrEmpty(Issuer.Binding))
                //{
                    //TODO: Implement IssuedTokenParameters
                    //parameters.IssuerBinding = ConfigLoader.LookupBinding(this.Issuer.Binding, this.Issuer.BindingConfiguration, this.EvaluationContext);
                //}
            }

            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.IssuerMetadata].ValueOrigin)
            {
                throw new PlatformNotSupportedException(nameof(IssuerMetadata));
                //TODO: Implement IssuedTokenParameters
                //parameters.IssuerMetadataAddress = ConfigLoader.LoadEndpointAddress(this.IssuerMetadata);
            }
        }

        internal void Copy(IssuedTokenParametersElement source)
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (null == source)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));
            }
            
            foreach (XmlElementElement xmlElement in source.AdditionalRequestParameters)
            {
                XmlElementElement newElement = new XmlElementElement();
                newElement.Copy(xmlElement);
                this.AdditionalRequestParameters.Add(newElement);
            }

            foreach (ClaimTypeElement c in source.ClaimTypeRequirements)
            {
                this.ClaimTypeRequirements.Add(new ClaimTypeElement(c.ClaimType, c.IsOptional));
            }

            KeySize = source.KeySize;
            KeyType = source.KeyType;
            TokenType = source.TokenType;
            DefaultMessageSecurityVersion = source.DefaultMessageSecurityVersion;
            UseStrTransform = source.UseStrTransform;

            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.Issuer].ValueOrigin)
            {
                Issuer.Copy(source.Issuer);
            }
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.IssuerMetadata].ValueOrigin)
            {
                IssuerMetadata.Copy(source.IssuerMetadata);
            }
        }

        internal IssuedSecurityTokenParameters Create(bool createTemplateOnly, SecurityKeyType templateKeyType)
        {
            IssuedSecurityTokenParameters result = new IssuedSecurityTokenParameters();
            if (!createTemplateOnly)
            {
                ApplyConfiguration(result);
            }
            else
            {
                result.KeyType = templateKeyType;
            }
            return result;
        }

        internal void InitializeFrom(IssuedSecurityTokenParameters source, bool initializeNestedBindings)
        {
            if (null == source)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));

            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeyType, source.KeyType);
            if (source.KeySize > 0)
            {
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeySize, source.KeySize);
            }
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TokenType, source.TokenType);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.UseStrTransform, source.UseStrTransform);

            if (source.IssuerAddress != null)
                Issuer.InitializeFrom(source.IssuerAddress);

            if (source.DefaultMessageSecurityVersion != null)
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.DefaultMessageSecurityVersion, source.DefaultMessageSecurityVersion);

            if (source.IssuerBinding != null && initializeNestedBindings)
            {
                Issuer.BindingConfiguration = Issuer.Address.ToString();


                //TODO: Implement IssuedTokenParameters
                throw new PlatformNotSupportedException(nameof(IssuedTokenParametersEndpointAddressElement));
                //string bindingSectionName;

                //BindingsSection.TryAdd(this.Issuer.BindingConfiguration,
                //    source.IssuerBinding,
                //    out bindingSectionName);
                //this.Issuer.Binding = bindingSectionName;
            }

            if (source.IssuerMetadataAddress != null)
            {
                IssuerMetadata.InitializeFrom(source.IssuerMetadataAddress);
            }

            foreach (XmlElement element in source.AdditionalRequestParameters)
            {
                this.AdditionalRequestParameters.Add(new XmlElementElement(element));
            }

            foreach (ClaimTypeRequirement c in source.ClaimTypeRequirements)
            {
                this.ClaimTypeRequirements.Add(new ClaimTypeElement(c.ClaimType, c.IsOptional));
            }

            //TODO: Implement IssuedTokenParameters
            //foreach (IssuedSecurityTokenParameters.AlternativeIssuerEndpoint alternativeIssuer in source.AlternativeIssuerEndpoints)
            //{
            //    IssuedTokenParametersElement element = new IssuedTokenParametersElement();
            //    element.Issuer.InitializeFrom(alternativeIssuer.IssuerAddress);
            //    if (initializeNestedBindings)
            //    {
            //        element.Issuer.BindingConfiguration = element.Issuer.Address.ToString();
            //        string bindingSectionName;
            //        BindingsSection.TryAdd(element.Issuer.BindingConfiguration,
            //            alternativeIssuer.IssuerBinding,
            //            out bindingSectionName);
            //        element.Issuer.Binding = bindingSectionName;
            //    }
            //    this.OptionalIssuedTokenParameters.Add(element);
            //}
        }

        protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
        {
            bool writeMe = base.SerializeToXmlElement(writer, elementName);
            bool writeComment = OptionalIssuedTokenParameters.Count > 0;
            if (writeComment && writer != null)
            {
                MemoryStream memoryStream = new MemoryStream();
                using (XmlTextWriter commentWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    commentWriter.Formatting = Formatting.Indented;
                    commentWriter.WriteStartElement(ConfigurationStrings.AlternativeIssuedTokenParameters);
                    foreach (IssuedTokenParametersElement element in OptionalIssuedTokenParameters)
                    {
                        element.SerializeToXmlElement(commentWriter, ConfigurationStrings.IssuedTokenParameters);
                    }
                    commentWriter.WriteEndElement();
                    commentWriter.Flush();
                    string commentString = new UTF8Encoding().GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                    writer.WriteComment(commentString.Substring(1, commentString.Length - 1));
                    commentWriter.Close();
                }
            }
            return writeMe || writeComment;
        }

        protected override void Unmerge(ConfigurationElement sourceElement, ConfigurationElement parentElement, ConfigurationSaveMode saveMode)
        {
            if (sourceElement is IssuedTokenParametersElement)
            {
                IssuedTokenParametersElement source = (IssuedTokenParametersElement)sourceElement;
                _optionalIssuedTokenParameters = source._optionalIssuedTokenParameters;
            }

            base.Unmerge(sourceElement, parentElement, saveMode);
        }
    }
}
