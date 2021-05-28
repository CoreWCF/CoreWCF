// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class BasicHttpBindingElement : HttpBindingBaseElement
    {
        public BasicHttpBindingElement(string name)
            : base(name)
        {
        }

        public BasicHttpBindingElement()
            : this(null)
        {
        }

        [ConfigurationProperty(ConfigurationStrings.Security)]
        public BasicHttpSecurityElement Security
        {
            get { return (BasicHttpSecurityElement)base[ConfigurationStrings.Security]; }
        }

        public override Binding CreateBinding()
        {
            var binding = new BasicHttpBinding(Security.Mode)
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
                ReaderQuotas = ReaderQuotas.Clone(),
            };

            return binding;
        }
    }
}
