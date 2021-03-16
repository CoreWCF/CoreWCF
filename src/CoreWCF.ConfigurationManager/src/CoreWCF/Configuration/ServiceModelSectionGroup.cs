// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Configuration
{
    public sealed class ServiceModelSectionGroup : ConfigurationSectionGroup
    {
        public ServiceModelSectionGroup()
        {
        }

        public BindingsSection Bindings
        {
            get { return (BindingsSection)this.Sections[ConfigurationStrings.BindingsSectionGroupName]; }
        }

        // todo implement
        //public ClientSection Client
        //{
        //    get { return (ClientSection)this.Sections[ConfigurationStrings.ClientSectionName]; }
        //}


        // todo implement
        //public ExtensionsSection Extensions
        //{
        //    get { return (ExtensionsSection)this.Sections[ConfigurationStrings.Extensions]; }
        //}

        public ServicesSection Services
        {
            get { return (ServicesSection)this.Sections[ConfigurationStrings.ServicesSectionName]; }
        }


        public static ServiceModelSectionGroup GetSectionGroup(System.Configuration.Configuration config)
        {
            if (config == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(config));
            }

            return (ServiceModelSectionGroup)config.SectionGroups[ConfigurationStrings.SectionGroupName];
        }
    }




    static class ServiceDefaults
    {
        internal static TimeSpan ServiceHostCloseTimeout { get { return TimeSpanHelper.FromSeconds(10, ServiceHostCloseTimeoutString); } }
        internal const string ServiceHostCloseTimeoutString = "00:00:10";
        internal static TimeSpan CloseTimeout { get { return TimeSpanHelper.FromMinutes(1, CloseTimeoutString); } }
        internal const string CloseTimeoutString = "00:01:00";
        internal static TimeSpan OpenTimeout { get { return TimeSpanHelper.FromMinutes(1, OpenTimeoutString); } }
        internal const string OpenTimeoutString = "00:01:00";
        internal static TimeSpan ReceiveTimeout { get { return TimeSpanHelper.FromMinutes(10, ReceiveTimeoutString); } }
        internal const string ReceiveTimeoutString = "00:10:00";
        internal static TimeSpan SendTimeout { get { return TimeSpanHelper.FromMinutes(1, SendTimeoutString); } }
        internal const string SendTimeoutString = "00:01:00";
        internal static TimeSpan TransactionTimeout { get { return TimeSpanHelper.FromMinutes(1, TransactionTimeoutString); } }
        internal const string TransactionTimeoutString = "00:00:00";
    }

    static class TimeSpanHelper
    {
        static public TimeSpan FromMinutes(int minutes, string text)
        {
            TimeSpan value = TimeSpan.FromTicks(TimeSpan.TicksPerMinute * minutes);
            Debug.Assert(value == TimeSpan.Parse(text, CultureInfo.InvariantCulture), "");
            return value;
        }
        static public TimeSpan FromSeconds(int seconds, string text)
        {
            TimeSpan value = TimeSpan.FromTicks(TimeSpan.TicksPerSecond * seconds);
            Debug.Assert(value == TimeSpan.Parse(text, CultureInfo.InvariantCulture), "");
            return value;
        }
        static public TimeSpan FromMilliseconds(int ms, string text)
        {
            TimeSpan value = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * ms);
            Debug.Assert(value == TimeSpan.Parse(text, CultureInfo.InvariantCulture), "");
            return value;
        }
        static public TimeSpan FromDays(int days, string text)
        {
            TimeSpan value = TimeSpan.FromTicks(TimeSpan.TicksPerDay * days);
            Debug.Assert(value == TimeSpan.Parse(text, CultureInfo.InvariantCulture), "");
            return value;
        }
    }

    internal class ConfigurationHelpers
    {
        internal static string GetSectionPath(string sectionName)
        {
            return string.Concat(ConfigurationStrings.SectionGroupName, "/", sectionName);
        }

        internal static string GetBindingsSectionPath(string sectionName)
        {
            return string.Concat(ConfigurationStrings.BindingsSectionGroupPath, "/", sectionName);
        }

    }
}
