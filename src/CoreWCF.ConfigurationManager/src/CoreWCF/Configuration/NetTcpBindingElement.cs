// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class NetTcpBindingElement : StandardBindingElement
    {
        public NetTcpBindingElement(string name)
            : base(name)
        {
        }

        public NetTcpBindingElement()
            : this(null)
        {
        }

        [ConfigurationProperty(ConfigurationStrings.TransactionFlow, DefaultValue = false)]
        public bool TransactionFlow
        {
            get { throw new PlatformNotSupportedException(); }
        }

        [ConfigurationProperty(ConfigurationStrings.TransferMode, DefaultValue = TransferMode.Buffered)]
        public TransferMode TransferMode
        {
            get { return (TransferMode)base[ConfigurationStrings.TransferMode]; }
            set { base[ConfigurationStrings.TransferMode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.TransactionProtocol, DefaultValue = "")]
        public string TransactionProtocol
        {
            get { throw new PlatformNotSupportedException(); }
        }

        [ConfigurationProperty(ConfigurationStrings.HostNameComparisonMode, DefaultValue = HostNameComparisonMode.StrongWildcard)]
        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return (HostNameComparisonMode)base[ConfigurationStrings.HostNameComparisonMode]; }
            set { base[ConfigurationStrings.HostNameComparisonMode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ListenBacklog, DefaultValue = 1)]
        [IntegerValidator(MinValue = 0)]
        public int ListenBacklog
        {
            get { throw new PlatformNotSupportedException(); }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferPoolSize, DefaultValue = 1L)]
        [LongValidator(MinValue = 0)]
        public long MaxBufferPoolSize
        {
            get { return (long)base[ConfigurationStrings.MaxBufferPoolSize]; }
            set { base[ConfigurationStrings.MaxBufferPoolSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBufferSize, DefaultValue = 65536)]
        [IntegerValidator(MinValue = 1)]
        public int MaxBufferSize
        {
            get { return (int)base[ConfigurationStrings.MaxBufferSize]; }
            set { base[ConfigurationStrings.MaxBufferSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxConnections, DefaultValue = 1)]
        [IntegerValidator(MinValue = 0)]
        public int MaxConnections
        {
            get { return (int)base[ConfigurationStrings.MaxConnections]; }
            set { base[ConfigurationStrings.MaxConnections] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxReceivedMessageSize, DefaultValue = 65536L)]
        [LongValidator(MinValue = 1)]
        public long MaxReceivedMessageSize
        {
            get { return (long)base[ConfigurationStrings.MaxReceivedMessageSize]; }
            set { base[ConfigurationStrings.MaxReceivedMessageSize] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.PortSharingEnabled, DefaultValue = false)]
        public bool PortSharingEnabled
        {
            get { throw new PlatformNotSupportedException(); }
        }

        [ConfigurationProperty(ConfigurationStrings.ReliableSession)]
        public string ReliableSession
        {
            get { throw new PlatformNotSupportedException(); }
        }

        [ConfigurationProperty(ConfigurationStrings.Security)]
        public NetTcpSecurityElement Security
        {
            get { return (NetTcpSecurityElement)base[ConfigurationStrings.Security]; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReaderQuotas)]
        public XmlDictionaryReaderQuotasElement ReaderQuotas
        {
            get { return (XmlDictionaryReaderQuotasElement)base[ConfigurationStrings.ReaderQuotas]; }
        }

        public override Binding CreateBinding()
        {
            var binding = new NetTcpBinding(Security.Mode)
            {
                Name = Name,
                MaxReceivedMessageSize = MaxReceivedMessageSize,
                MaxBufferSize = MaxBufferSize,
                MaxBufferPoolSize = MaxBufferPoolSize,
                MaxConnections = MaxConnections,                
                ReceiveTimeout = ReceiveTimeout,
                OpenTimeout = OpenTimeout,
                CloseTimeout = CloseTimeout,
                SendTimeout = SendTimeout,
                ReaderQuotas = ReaderQuotas.Clone(),
                TransferMode = TransferMode,
                HostNameComparisonMode = HostNameComparisonMode
            };

            //this.ReliableSession.ApplyConfiguration(nptBinding.ReliableSession);
            Security.ApplyConfiguration(binding.Security);
            //this.ReaderQuotas.ApplyConfiguration(nptBinding.ReaderQuotas);
            return binding;
        }
    }
}
