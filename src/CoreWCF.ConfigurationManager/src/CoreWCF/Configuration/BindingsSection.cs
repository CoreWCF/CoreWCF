// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;

namespace CoreWCF.Configuration
{
    public sealed class BindingsSection : ConfigurationSection
    {
        [ConfigurationProperty(ConfigurationStrings.BasicHttpBindingCollectionElementName, Options = ConfigurationPropertyOptions.None)]
        public BasicHttpBindingCollectionElement BasicHttpBinding
        {
            get { return (BasicHttpBindingCollectionElement)base[ConfigurationStrings.BasicHttpBindingCollectionElementName]; }
        }

        [ConfigurationProperty(ConfigurationStrings.NetTcpBindingCollectionElementName, Options = ConfigurationPropertyOptions.None)]
        public NetTcpBindingCollectionElement NetTcpBinding
        {
            get { return (NetTcpBindingCollectionElement)base[ConfigurationStrings.NetTcpBindingCollectionElementName]; }
        }

        [ConfigurationProperty(ConfigurationStrings.NetHttpBindingCollectionElementName, Options = ConfigurationPropertyOptions.None)]
        public NetHttpBindingCollectionElement NetHttpBinding
        {
            get { return (NetHttpBindingCollectionElement)base[ConfigurationStrings.NetHttpBindingCollectionElementName]; }
        }

        [ConfigurationProperty(ConfigurationStrings.WsHttpBindingCollectionElementName, Options = ConfigurationPropertyOptions.None)]
        public WsHttpBindingCollectionElement wsHttpBinding
        {
            get { return (WsHttpBindingCollectionElement)base[ConfigurationStrings.WsHttpBindingCollectionElementName]; }
        }

        [ConfigurationProperty(ConfigurationStrings.CustomBindingCollectionElementName, Options = ConfigurationPropertyOptions.None)]
        public CustomBindingCollectionElement CustomBinding
        {
            get { return (CustomBindingCollectionElement)base[ConfigurationStrings.CustomBindingCollectionElementName]; }
        }
    }
}
