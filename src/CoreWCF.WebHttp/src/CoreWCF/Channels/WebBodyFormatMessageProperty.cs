// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace CoreWCF.Channels
{
    public sealed class WebBodyFormatMessageProperty : IMessageProperty
    {
        public const string Name = "WebBodyFormatMessageProperty";

        private static WebBodyFormatMessageProperty s_jsonProperty;
        private static WebBodyFormatMessageProperty s_xmlProperty;
        private static WebBodyFormatMessageProperty s_rawProperty;

        public WebBodyFormatMessageProperty(WebContentFormat format)
        {
            if (format == WebContentFormat.Default)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.DefaultContentFormatNotAllowedInProperty)));
            }

            Format = format;
        }

        public WebContentFormat Format { get; }

        internal static WebBodyFormatMessageProperty JsonProperty
        {
            get
            {
                if (s_jsonProperty == null)
                {
                    s_jsonProperty = new WebBodyFormatMessageProperty(WebContentFormat.Json);
                }

                return s_jsonProperty;
            }
        }

        internal static WebBodyFormatMessageProperty XmlProperty
        {
            get
            {
                if (s_xmlProperty == null)
                {
                    s_xmlProperty = new WebBodyFormatMessageProperty(WebContentFormat.Xml);
                }

                return s_xmlProperty;
            }
        }

        internal static WebBodyFormatMessageProperty RawProperty
        {
            get
            {
                if (s_rawProperty == null)
                {
                    s_rawProperty = new WebBodyFormatMessageProperty(WebContentFormat.Raw);
                }

                return s_rawProperty;
            }
        }

        public IMessageProperty CreateCopy() => this;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, SR.Format(SR.WebBodyFormatPropertyToString, Format.ToString()));
    }
}
