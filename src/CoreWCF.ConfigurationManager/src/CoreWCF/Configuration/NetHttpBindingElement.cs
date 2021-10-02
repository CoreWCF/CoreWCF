// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class NetHttpBindingElement : HttpBindingBaseElement
    {
        public NetHttpBindingElement(string name)
            : base(name)
        {
        }

        public NetHttpBindingElement()
            : this(null)
        {
        }

        [ConfigurationProperty(ConfigurationStrings.MessageEncoding, DefaultValue = NetHttpMessageEncoding.Binary)]       
        public NetHttpMessageEncoding MessageEncoding
        {
            get { return (NetHttpMessageEncoding)base[ConfigurationStrings.MessageEncoding]; }
            set { base[ConfigurationStrings.MessageEncoding] = value; }
        }


        [ConfigurationProperty(ConfigurationStrings.ReliableSession)]
        public string ReliableSession
        {
            get { throw new PlatformNotSupportedException(); }
        }


        [ConfigurationProperty(ConfigurationStrings.Security)]
        public BasicHttpSecurityElement Security
        {
            get { return (BasicHttpSecurityElement)base[ConfigurationStrings.Security]; }
        }

        // todo how to implement ? WebSocketSettings is internal
        [ConfigurationProperty(ConfigurationStrings.WebSocketSettingsSectionName)]
        public NetHttpWebSocketTransportSettingsElement WebSocketSettings
        {
            get { return (NetHttpWebSocketTransportSettingsElement)base[ConfigurationStrings.WebSocketSettingsSectionName]; }
            set { base[ConfigurationStrings.WebSocketSettingsSectionName] = value; }
        }

        public override Binding CreateBinding()
        {
            var binding = new NetHttpBinding(Security.Mode)
            {
                Name = Name,
                MaxReceivedMessageSize = MaxReceivedMessageSize,
                MaxBufferSize = MaxBufferSize,
                ReceiveTimeout = ReceiveTimeout,
                CloseTimeout = CloseTimeout,
                OpenTimeout = OpenTimeout,
                SendTimeout = SendTimeout,
                TransferMode = TransferMode,
                TextEncoding = TextEncoding,
                MessageEncoding = MessageEncoding,
                ReaderQuotas = ReaderQuotas.Clone(),
            };

            binding.MessageEncoding = MessageEncoding;
            //WebSocketSettings.ApplyConfiguration(netHttpBinding.WebSocketSettings);
            // this.ReliableSession.ApplyConfiguration(netHttpBinding.ReliableSession);
            Security.ApplyConfiguration(binding.Security);
            return binding;
        }
    }
}
