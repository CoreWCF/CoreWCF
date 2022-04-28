// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Specialized;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal abstract class UriTemplateQueryValue
    {
        protected UriTemplateQueryValue(UriTemplatePartType nature)
        {
            Nature = nature;
        }

        public static UriTemplateQueryValue Empty { get; } = new EmptyUriTemplateQueryValue();

        public UriTemplatePartType Nature { get; }

        public static UriTemplateQueryValue CreateFromUriTemplate(string value, UriTemplate template)
        {
            // Checking for empty value
            if (value == null)
            {
                return Empty;
            }

            // Identifying the type of value - Literal|Compound|Variable
            switch (UriTemplateHelpers.IdentifyPartType(value))
            {
                case UriTemplatePartType.Literal:
                    return UriTemplateLiteralQueryValue.CreateFromUriTemplate(value);

                case UriTemplatePartType.Compound:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                        SR.UTQueryCannotHaveCompoundValue, template._originalTemplate)));

                case UriTemplatePartType.Variable:
                    return new UriTemplateVariableQueryValue(template.AddQueryVariable(value.Substring(1, value.Length - 2)));

                default:
                    Fx.Assert("Invalid value from IdentifyStringNature");
                    return null;
            }
        }

        public static bool IsNullOrEmpty(UriTemplateQueryValue utqv)
        {
            if (utqv == null)
            {
                return true;
            }

            if (utqv == Empty)
            {
                return true;
            }

            return false;
        }

        public abstract void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query);

        public abstract bool IsEquivalentTo(UriTemplateQueryValue other);

        public abstract void Lookup(string value, NameValueCollection boundParameters);

        internal class EmptyUriTemplateQueryValue : UriTemplateQueryValue
        {
            public EmptyUriTemplateQueryValue()
                : base(UriTemplatePartType.Literal)
            {
            }

            public override void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query)
            {
                query.AppendFormat("&{0}", UrlUtility.UrlEncode(keyName, Encoding.UTF8));
            }

            public override bool IsEquivalentTo(UriTemplateQueryValue other) => other == Empty;

            public override void Lookup(string value, NameValueCollection boundParameters)
            {
                Fx.Assert(string.IsNullOrEmpty(value), "shouldn't have a value");
            }
        }
    }
}
