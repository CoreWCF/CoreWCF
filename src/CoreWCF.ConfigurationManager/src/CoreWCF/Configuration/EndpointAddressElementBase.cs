// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    public class EndpointAddressElementBase : ServiceModelConfigurationElement
    {
        protected EndpointAddressElementBase()
        {
        }

        [ConfigurationProperty(ConfigurationStrings.Address, DefaultValue = null, Options = ConfigurationPropertyOptions.IsRequired)]
        public Uri Address
        {
            get { return (Uri)base[ConfigurationStrings.Address]; }
            set { base[ConfigurationStrings.Address] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Headers)]
        public AddressHeaderCollectionElement Headers
        {
            get { return (AddressHeaderCollectionElement)base[ConfigurationStrings.Headers]; }
        }

        //TODO: Add Identity Support
        //[ConfigurationProperty(ConfigurationStrings.Identity)]
        //public IdentityElement Identity
        //{
        //    get { return (IdentityElement)base[ConfigurationStrings.Identity]; }
        //}


        internal protected void Copy(EndpointAddressElementBase source)
        {
            if (this.IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (null == source)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }

            this.Address = source.Address;
            this.Headers.Headers = source.Headers.Headers;
            //TODO: Add Identity Support
            //PropertyInformationCollection properties = source.ElementInformation.Properties;
            //if (properties[ConfigurationStrings.Identity].ValueOrigin != PropertyValueOrigin.Default)
            //{
            //    this.Identity.Copy(source.Identity);
            //}
        }

        public void InitializeFrom(EndpointAddress endpointAddress)
        {
            if (null == endpointAddress)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("endpointAddress");
            }

            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.Address, endpointAddress.Uri);
            this.Headers.InitializeFrom(endpointAddress.Headers);
            //TODO: Add Identity Support
            //if (null != endpointAddress.Identity)
            //{
            //    this.Identity.InitializeFrom(endpointAddress.Identity);
            //}
        }
    }
}
