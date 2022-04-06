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
    internal sealed class IssuedTokenParametersElement : ServiceModelConfigurationElement
    {
        Collection<IssuedTokenParametersElement> optionalIssuedTokenParameters = null;
        
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

        [ConfigurationProperty(ConfigurationStrings.KeyType, DefaultValue = IssuedSecurityTokenParameters.DefaultKeyType)]
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
                if (this.IsReadOnly())
                {
                    Fx.Assert("IssuedTokenParametersElement.OptionalIssuedTokenParameters should only be called by Admin APIs");
                    DiagnosticUtility.FailFast("IssuedTokenParametersElement.OptionalIssuedTokenParameters should only be called by Admin APIs");
                }

                // No need to worry about a race condition here-- this method is not meant to be called by multi-threaded
                // apps. It is only supposed to be called by svcutil and single threaded equivalents.
                if (this.optionalIssuedTokenParameters == null)
                {
                    this.optionalIssuedTokenParameters = new Collection<IssuedTokenParametersElement>();
                }
                return this.optionalIssuedTokenParameters;
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("parameters"));

            if (this.AdditionalRequestParameters != null)
            {
                foreach (XmlElementElement e in this.AdditionalRequestParameters)
                {
                    parameters.AdditionalRequestParameters.Add(e.XmlElement);
                }
            }

            if (this.ClaimTypeRequirements != null)
            {
                foreach (ClaimTypeElement c in this.ClaimTypeRequirements)
                {
                    parameters.ClaimTypeRequirements.Add(new ClaimTypeRequirement(c.ClaimType, c.IsOptional));
                }
            }

            parameters.KeySize = this.KeySize;
            //TODO: This is an internal property
            //parameters.KeyType = this.KeyType;
            parameters.DefaultMessageSecurityVersion = this.DefaultMessageSecurityVersion;
            parameters.UseStrTransform = this.UseStrTransform;

            if (!string.IsNullOrEmpty(this.TokenType))
            {
                parameters.TokenType = this.TokenType;
            }
            if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.Issuer].ValueOrigin)
            {
                //TODO: Implement IssuedTokenParameters
                //this.Issuer.Validate();

                //TODO: Implement IssuedTokenParameters
                //parameters.IssuerAddress = ConfigLoader.LoadEndpointAddress(this.Issuer);

                if (!string.IsNullOrEmpty(this.Issuer.Binding))
                {
                    //TODO: Implement IssuedTokenParameters
                    //parameters.IssuerBinding = ConfigLoader.LookupBinding(this.Issuer.Binding, this.Issuer.BindingConfiguration, this.EvaluationContext);
                }
            }

            if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.IssuerMetadata].ValueOrigin)
            {
                //TODO: Implement IssuedTokenParameters
                //parameters.IssuerMetadataAddress = ConfigLoader.LoadEndpointAddress(this.IssuerMetadata);
            }
        }

        internal void Copy(IssuedTokenParametersElement source)
        {
            if (this.IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (null == source)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }

            //TODO: Implement IssuedTokenParameters
            //foreach (XmlElementElement xmlElement in source.AdditionalRequestParameters)
            //{
            //    XmlElementElement newElement = new XmlElementElement();
            //    newElement.Copy(xmlElement);
            //    this.AdditionalRequestParameters.Add(newElement);
            //}

            //TODO: Implement IssuedTokenParameters
            //foreach (ClaimTypeElement c in source.ClaimTypeRequirements)
            //{
            //    this.ClaimTypeRequirements.Add(new ClaimTypeElement(c.ClaimType, c.IsOptional));
            //}

            this.KeySize = source.KeySize;
            this.KeyType = source.KeyType;
            this.TokenType = source.TokenType;
            this.DefaultMessageSecurityVersion = source.DefaultMessageSecurityVersion;
            this.UseStrTransform = source.UseStrTransform;

            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.Issuer].ValueOrigin)
            {
                this.Issuer.Copy(source.Issuer);
            }
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.IssuerMetadata].ValueOrigin)
            {
                this.IssuerMetadata.Copy(source.IssuerMetadata);
            }
        }

        internal IssuedSecurityTokenParameters Create(bool createTemplateOnly, SecurityKeyType templateKeyType)
        {
            IssuedSecurityTokenParameters result = new IssuedSecurityTokenParameters();
            if (!createTemplateOnly)
            {
                this.ApplyConfiguration(result);
            }
            else
            {
                //TODO: This is an internal property
                //result.KeyType = templateKeyType;
            }
            return result;
        }

        internal void InitializeFrom(IssuedSecurityTokenParameters source, bool initializeNestedBindings)
        {
            if (null == source)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");

            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeyType, source.KeyType);
            if (source.KeySize > 0)
            {
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeySize, source.KeySize);
            }
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.TokenType, source.TokenType);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.UseStrTransform, source.UseStrTransform);

            if (source.IssuerAddress != null)
                this.Issuer.InitializeFrom(source.IssuerAddress);

            if (source.DefaultMessageSecurityVersion != null)
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.DefaultMessageSecurityVersion, source.DefaultMessageSecurityVersion);

            if (source.IssuerBinding != null && initializeNestedBindings)
            {
                this.Issuer.BindingConfiguration = this.Issuer.Address.ToString();

                //TODO: Implement IssuedTokenParameters
                //string bindingSectionName;
                
                //BindingsSection.TryAdd(this.Issuer.BindingConfiguration,
                //    source.IssuerBinding,
                //    out bindingSectionName);
                //this.Issuer.Binding = bindingSectionName;
            }

            if (source.IssuerMetadataAddress != null)
            {
                this.IssuerMetadata.InitializeFrom(source.IssuerMetadataAddress);
            }

            foreach (XmlElement element in source.AdditionalRequestParameters)
            {
                //TODO: Implement IssuedTokenParameters
                //this.AdditionalRequestParameters.Add(new XmlElementElement(element));
            }

            foreach (ClaimTypeRequirement c in source.ClaimTypeRequirements)
            {
                //TODO: Implement IssuedTokenParameters
                //this.ClaimTypeRequirements.Add(new ClaimTypeElement(c.ClaimType, c.IsOptional));
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
            bool writeComment = this.OptionalIssuedTokenParameters.Count > 0;
            if (writeComment && writer != null)
            {
                MemoryStream memoryStream = new MemoryStream();
                using (XmlTextWriter commentWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    commentWriter.Formatting = Formatting.Indented;
                    commentWriter.WriteStartElement(ConfigurationStrings.AlternativeIssuedTokenParameters);
                    foreach (IssuedTokenParametersElement element in this.OptionalIssuedTokenParameters)
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
                this.optionalIssuedTokenParameters = source.optionalIssuedTokenParameters;
            }

            base.Unmerge(sourceElement, parentElement, saveMode);
        }
    }
}
