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

        //[ConfigurationProperty(ConfigurationStrings.TransactionFlow, DefaultValue = TransactionFlowDefaults.Transactions)]
        //public bool TransactionFlow
        //{
        //    get { return (bool)base[ConfigurationStrings.TransactionFlow]; }
        //    set { base[ConfigurationStrings.TransactionFlow] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.TransferMode, DefaultValue = ConnectionOrientedTransportDefaults.TransferMode)]
        //[ServiceModelEnumValidator(typeof(TransferModeHelper))]
        //public TransferMode TransferMode
        //{
        //    get { return (TransferMode)base[ConfigurationStrings.TransferMode]; }
        //    set { base[ConfigurationStrings.TransferMode] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.TransactionProtocol, DefaultValue = TransactionFlowDefaults.TransactionProtocolString)]
        //[TypeConverter(typeof(TransactionProtocolConverter))]
        //public TransactionProtocol TransactionProtocol
        //{
        //    get { return (TransactionProtocol)base[ConfigurationStrings.TransactionProtocol]; }
        //    set { base[ConfigurationStrings.TransactionProtocol] = value; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.HostNameComparisonMode, DefaultValue = ConnectionOrientedTransportDefaults.HostNameComparisonMode)]
        //[ServiceModelEnumValidator(typeof(HostNameComparisonModeHelper))]
        //public HostNameComparisonMode HostNameComparisonMode
        //{
        //    get { return (HostNameComparisonMode)base[ConfigurationStrings.HostNameComparisonMode]; }
        //    set { base[ConfigurationStrings.HostNameComparisonMode] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.ListenBacklog, DefaultValue = 1)]
        [IntegerValidator(MinValue = 0)]       
        public int ListenBacklog
        {
            get { throw new PlatformNotSupportedException("Not Support"); }
            //set { throw new Exception("Not Support"); }
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
            get { return (bool)base[ConfigurationStrings.PortSharingEnabled]; }
            set { base[ConfigurationStrings.PortSharingEnabled] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReaderQuotas)]
        public XmlDictionaryReaderQuotasElement ReaderQuotas
        {
            get { return (XmlDictionaryReaderQuotasElement)base[ConfigurationStrings.ReaderQuotas]; }
        }

        public override Binding CreateBinding()
        {
            var binding = new NetTcpBinding()
            {
                Name = Name,
                MaxReceivedMessageSize = MaxReceivedMessageSize,
                MaxBufferSize = MaxBufferSize,
                ReceiveTimeout = ReceiveTimeout,
                ReaderQuotas = ReaderQuotas.Clone(),
            };

            return binding;
        }

        //[ConfigurationProperty(ConfigurationStrings.ReliableSession)]
        //public StandardBindingOptionalReliableSessionElement ReliableSession
        //{
        //    get { return (StandardBindingOptionalReliableSessionElement)base[ConfigurationStrings.ReliableSession]; }
        //}

        //[ConfigurationProperty(ConfigurationStrings.Security)]
        //public NetTcpSecurityElement Security
        //{
        //    get { return (NetTcpSecurityElement)base[ConfigurationStrings.Security]; }
        //}
    }
}
