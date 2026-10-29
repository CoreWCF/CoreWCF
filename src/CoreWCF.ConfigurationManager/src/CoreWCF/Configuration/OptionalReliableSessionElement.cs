// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Configuration;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class OptionalReliableSessionElement : ServiceModelConfigurationElement
    {
        [ConfigurationProperty(ConfigurationStrings.Enabled, DefaultValue = false)]
        public bool Enabled
        {
            get { return (bool)base[ConfigurationStrings.Enabled]; }
            set { base[ConfigurationStrings.Enabled] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.Ordered, DefaultValue = true)]
        public bool Ordered
        {
            get { return (bool)base[ConfigurationStrings.Ordered]; }
            set { base[ConfigurationStrings.Ordered] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.InactivityTimeout, DefaultValue = "00:10:00")]
        [TypeConverter(typeof(TimeSpanOrInfiniteConverter))]
        public TimeSpan InactivityTimeout
        {
            get { return (TimeSpan)base[ConfigurationStrings.InactivityTimeout]; }
            set { base[ConfigurationStrings.InactivityTimeout] = value; }
        }

        internal void ApplyConfiguration(OptionalReliableSession reliableSession)
        {
            if (reliableSession == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reliableSession));
            }

            reliableSession.Enabled = Enabled;
            reliableSession.Ordered = Ordered;
            reliableSession.InactivityTimeout = InactivityTimeout;
        }
    }
}
