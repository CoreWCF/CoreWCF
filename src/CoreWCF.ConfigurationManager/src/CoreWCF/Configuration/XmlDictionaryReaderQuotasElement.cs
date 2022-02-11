// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Xml;

namespace CoreWCF.Configuration
{
    public class XmlDictionaryReaderQuotasElement : ConfigurationElement
    {
        // for all properties, a value of 0 means "just use the default"
        [ConfigurationProperty(ConfigurationStrings.MaxDepth, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxDepth
        {
            get { return (int)base[ConfigurationStrings.MaxDepth]; }
            set { base[ConfigurationStrings.MaxDepth] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxStringContentLength, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxStringContentLength
        {
            get { return (int)base[ConfigurationStrings.MaxStringContentLength]; }
            set { base[ConfigurationStrings.MaxStringContentLength] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxArrayLength, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxArrayLength
        {
            get { return (int)base[ConfigurationStrings.MaxArrayLength]; }
            set { base[ConfigurationStrings.MaxArrayLength] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxBytesPerRead, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxBytesPerRead
        {
            get { return (int)base[ConfigurationStrings.MaxBytesPerRead]; }
            set { base[ConfigurationStrings.MaxBytesPerRead] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MaxNameTableCharCount, DefaultValue = 0)]
        [IntegerValidator(MinValue = 0)]
        public int MaxNameTableCharCount
        {
            get { return (int)base[ConfigurationStrings.MaxNameTableCharCount]; }
            set { base[ConfigurationStrings.MaxNameTableCharCount] = value; }
        }

        internal void ApplyConfiguration(XmlDictionaryReaderQuotas readerQuotas)
        {
            if (readerQuotas == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(readerQuotas));
            }
            if (MaxDepth != 0)
            {
                readerQuotas.MaxDepth = MaxDepth;
            }
            if (MaxStringContentLength != 0)
            {
                readerQuotas.MaxStringContentLength = MaxStringContentLength;
            }
            if (MaxArrayLength != 0)
            {
                readerQuotas.MaxArrayLength = MaxArrayLength;
            }
            if (MaxBytesPerRead != 0)
            {
                readerQuotas.MaxBytesPerRead = MaxBytesPerRead;
            }
            if (MaxNameTableCharCount != 0)
            {
                readerQuotas.MaxNameTableCharCount = MaxNameTableCharCount;
            }
        }

        internal XmlDictionaryReaderQuotas Clone()
        {
            var readerQuotas = new XmlDictionaryReaderQuotas();
            ApplyConfiguration(readerQuotas);
            return readerQuotas;
        }

        //internal void InitializeFrom(XmlDictionaryReaderQuotas readerQuotas)
        //{
        //    if (readerQuotas == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(readerQuotas));
        //    }
        //    if (readerQuotas.MaxDepth != EncoderDefaults.MaxDepth)
        //    {
        //        SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxDepth, readerQuotas.MaxDepth);
        //    }
        //    if (readerQuotas.MaxStringContentLength != EncoderDefaults.MaxStringContentLength)
        //    {
        //        SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxStringContentLength, readerQuotas.MaxStringContentLength);
        //    }
        //    if (readerQuotas.MaxArrayLength != EncoderDefaults.MaxArrayLength)
        //    {
        //        SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxArrayLength, readerQuotas.MaxArrayLength);
        //    }
        //    if (readerQuotas.MaxBytesPerRead != EncoderDefaults.MaxBytesPerRead)
        //    {
        //        SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxBytesPerRead, readerQuotas.MaxBytesPerRead);
        //    }
        //    if (readerQuotas.MaxNameTableCharCount != EncoderDefaults.MaxNameTableCharCount)
        //    {
        //        SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MaxNameTableCharCount, readerQuotas.MaxNameTableCharCount);
        //    }
        //}
    }

    internal static class EncoderDefaults
    {
        internal const int MaxReadPoolSize = 64;
        internal const int MaxWritePoolSize = 16;

        internal const int MaxDepth = 32;
        internal const int MaxStringContentLength = 8192;
        internal const int MaxArrayLength = 16384;
        internal const int MaxBytesPerRead = 4096;
        internal const int MaxNameTableCharCount = 16384;

    }

}
