// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class WsHttpBindingElement : WsHttpBindingBaseElement
    {
        public WsHttpBindingElement(string name)
        : base(name)
        {
        }

        public WsHttpBindingElement()
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
        public WsHttpSecurityElement Security
        {
            get { return (WsHttpSecurityElement)base[ConfigurationStrings.Security]; }
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

            //binding.AllowCookies = this.AllowCookies;
            Security.ApplyConfiguration(binding.Security);
            return binding;
        }
    }
}
