// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public abstract class StandardBindingElement : ServiceModelConfigurationElement, IDefaultCommunicationTimeouts, IStandardBindingElement
    {
        protected StandardBindingElement()
            : this(null)
        {
        }

        protected StandardBindingElement(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Name, Options = ConfigurationPropertyOptions.IsKey)]
        [StringValidator(MinLength = 0)]
        public string Name
        {
            get { return (string)base[ConfigurationStrings.Name]; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                base[ConfigurationStrings.Name] = value;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.CloseTimeout, DefaultValue = ServiceDefaults.CloseTimeoutString)]    
        public TimeSpan CloseTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.CloseTimeout]; }
            set { base[ConfigurationStrings.CloseTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.OpenTimeout, DefaultValue = ServiceDefaults.OpenTimeoutString)]    
        public TimeSpan OpenTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.OpenTimeout]; }
            set { base[ConfigurationStrings.OpenTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ReceiveTimeout, DefaultValue = ServiceDefaults.ReceiveTimeoutString)]       
        public TimeSpan ReceiveTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.ReceiveTimeout]; }
            set { base[ConfigurationStrings.ReceiveTimeout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SendTimeout, DefaultValue = ServiceDefaults.SendTimeoutString)]     
        public TimeSpan SendTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.SendTimeout]; }
            set { base[ConfigurationStrings.SendTimeout] = value; }
        }

        public abstract Binding CreateBinding();

    }
}
