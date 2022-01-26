// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal class UriTemplateVariablePathSegment : UriTemplatePathSegment
    {
        public UriTemplateVariablePathSegment(string originalSegment, bool endsWithSlash, string varName)
            : base(originalSegment, UriTemplatePartType.Variable, endsWithSlash)
        {
            Fx.Assert(!string.IsNullOrEmpty(varName), "bad variable segment");
            VarName = varName;
        }

        public string VarName { get; }

        public override void Bind(string[] values, ref int valueIndex, StringBuilder path)
        {
            Fx.Assert(valueIndex < values.Length, "Not enough values to bind");
            if (EndsWithSlash)
            {
                path.AppendFormat("{0}/", values[valueIndex++]);
            }
            else
            {
                path.Append(values[valueIndex++]);
            }
        }

        public override bool IsEquivalentTo(UriTemplatePathSegment other, bool ignoreTrailingSlash)
        {
            if (other == null)
            {
                Fx.Assert("why would we ever call this?");
                return false;
            }

            if (!ignoreTrailingSlash && (EndsWithSlash != other.EndsWithSlash))
            {
                return false;
            }

            return (other.Nature == UriTemplatePartType.Variable);
        }

        public override bool IsMatch(UriTemplateLiteralPathSegment segment, bool ignoreTrailingSlash)
        {
            if (!ignoreTrailingSlash && (EndsWithSlash != segment.EndsWithSlash))
            {
                return false;
            }

            return (!segment.IsNullOrEmpty());
        }

        public override void Lookup(string segment, NameValueCollection boundParameters)
        {
            Fx.Assert(!string.IsNullOrEmpty(segment), "How can that be? Lookup is expected to be called after IsMatch");

            boundParameters.Add(VarName, segment);
        }
    }
}
