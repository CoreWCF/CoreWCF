// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class StandardBindingCollectionElement<TStandardBinding, TBindingConfiguration> : BindingCollectionElement
        where TStandardBinding : Binding
        where TBindingConfiguration : ConfigurationElement, IDefaultCommunicationTimeouts, IStandardBindingElement, new()
    {

        [ConfigurationProperty(ConfigurationStrings.DefaultCollectionName, Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public StandardBindingElementCollection<TBindingConfiguration> Bindings
        {
            get { return (StandardBindingElementCollection<TBindingConfiguration>)base[ConfigurationStrings.DefaultCollectionName]; }
        }
    }
}
