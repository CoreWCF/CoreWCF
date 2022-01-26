// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal class UriTemplateVariableQueryValue : UriTemplateQueryValue
    {
        private readonly string _varName;

        public UriTemplateVariableQueryValue(string varName)
            : base(UriTemplatePartType.Variable)
        {
            Fx.Assert(!string.IsNullOrEmpty(varName), "bad variable segment");
            _varName = varName;
        }

        public override void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query)
        {
            Fx.Assert(valueIndex < values.Length, "Not enough values to bind");

            if (values[valueIndex] == null)
            {
                valueIndex++;
            }
            else
            {
                query.AppendFormat("&{0}={1}", UrlUtility.UrlEncode(keyName, Encoding.UTF8), UrlUtility.UrlEncode(values[valueIndex++], Encoding.UTF8));
            }
        }

        public override bool IsEquivalentTo(UriTemplateQueryValue other)
        {
            if (other == null)
            {
                Fx.Assert("why would we ever call this?");

                return false;
            }

            return other.Nature == UriTemplatePartType.Variable;
        }

        public override void Lookup(string value, NameValueCollection boundParameters)
        {
            boundParameters.Add(_varName, value);
        }
    }
}
