// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class WSHttpBindingElement : WSHttpBindingBaseElement
    {
        public WSHttpBindingElement(string name)
        : base(name)
        {
        }

        public WSHttpBindingElement()
            : this(null)
        {
        }

        [ConfigurationProperty(ConfigurationStrings.AllowCookies, DefaultValue = false)]
        public bool AllowCookies
        {
            get { return (bool)base[ConfigurationStrings.AllowCookies]; }
            set { base[ConfigurationStrings.AllowCookies] = value; }

        }

        [ConfigurationProperty(ConfigurationStrings.Security)]
        public WSHttpSecurityElement Security
        {
            get { return (WSHttpSecurityElement)base[ConfigurationStrings.Security]; }
        }

        public override Binding CreateBinding()
        {
            var binding = new WSHttpBinding(Security.Mode)
            {
                CloseTimeout = CloseTimeout,
                MaxBufferPoolSize = MaxBufferPoolSize,
                MaxReceivedMessageSize = MaxReceivedMessageSize,
                Name = Name,
                OpenTimeout = OpenTimeout,
                ReaderQuotas = ReaderQuotas.Clone(),
                ReceiveTimeout = ReceiveTimeout,
                SendTimeout = SendTimeout,              
            };

            return binding;
        }
    }
}
