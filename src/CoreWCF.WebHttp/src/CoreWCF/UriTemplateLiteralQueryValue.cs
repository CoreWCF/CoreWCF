// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Specialized;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF
{
    // thin wrapper around string; use type system to help ensure we
    // are doing canonicalization right/consistently
    internal class UriTemplateLiteralQueryValue : UriTemplateQueryValue, IComparable<UriTemplateLiteralQueryValue>
    {
        private readonly string _value; // an unescaped representation

        private UriTemplateLiteralQueryValue(string value)
            : base(UriTemplatePartType.Literal)
        {
            Fx.Assert(value != null, "bad literal value");
            _value = value;
        }

        public static UriTemplateLiteralQueryValue CreateFromUriTemplate(string value) => new UriTemplateLiteralQueryValue(UrlUtility.UrlDecode(value, Encoding.UTF8));

        public string AsEscapedString() => UrlUtility.UrlEncode(_value, Encoding.UTF8);

        public string AsRawUnescapedString() => _value;

        public override void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query)
        {
            query.AppendFormat("&{0}={1}", UrlUtility.UrlEncode(keyName, Encoding.UTF8), AsEscapedString());
        }

        public int CompareTo(UriTemplateLiteralQueryValue other) => string.Compare(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj)
        {
            if (!(obj is UriTemplateLiteralQueryValue lqv))
            {
                Fx.Assert("why would we ever call this?");
                return false;
            }
            else
            {
                return _value == lqv._value;
            }
        }

        public override int GetHashCode() => _value.GetHashCode();

        public override bool IsEquivalentTo(UriTemplateQueryValue other)
        {
            if (other == null)
            {
                Fx.Assert("why would we ever call this?");
                return false;
            }

            if (other.Nature != UriTemplatePartType.Literal)
            {
                return false;
            }

            UriTemplateLiteralQueryValue otherAsLiteral = other as UriTemplateLiteralQueryValue;
            Fx.Assert(otherAsLiteral != null, "The nature requires that this will be OK");

            return CompareTo(otherAsLiteral) == 0;
        }

        public override void Lookup(string value, NameValueCollection boundParameters)
        {
            Fx.Assert(string.Compare(_value, value, StringComparison.Ordinal) == 0, "How can that be?");
        }
    }
}
