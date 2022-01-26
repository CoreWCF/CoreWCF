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
    internal class UriTemplateLiteralPathSegment : UriTemplatePathSegment, IComparable<UriTemplateLiteralPathSegment>
    {
        // segment doesn't store trailing slash
        private readonly string _segment;
        private static readonly Uri s_dummyUri = new Uri("http://localhost");

        private UriTemplateLiteralPathSegment(string segment)
            : base(segment, UriTemplatePartType.Literal, segment.EndsWith("/", StringComparison.Ordinal))
        {
            Fx.Assert(segment != null, "bad literal segment");
            if (EndsWithSlash)
            {
                _segment = segment.Remove(segment.Length - 1);
            }
            else
            {
                _segment = segment;
            }
        }

        public static new UriTemplateLiteralPathSegment CreateFromUriTemplate(string segment, UriTemplate template)
        {
            // run it through UriBuilder to escape-if-necessary it
            if (string.Compare(segment, "/", StringComparison.Ordinal) == 0)
            {
                // running an empty segment through UriBuilder has unexpected/wrong results
                return new UriTemplateLiteralPathSegment("/");
            }

            if (segment.IndexOf(UriTemplate.WildcardPath, StringComparison.Ordinal) != -1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                    SR.Format(SR.UTInvalidWildcardInVariableOrLiteral, template._originalTemplate, UriTemplate.WildcardPath)));
            }

            // '*' is not usually escaped by the Uri\UriBuilder to %2a, since we forbid passing a
            // clear character and the workaroud is to pass the escaped form, we should replace the
            // escaped form with the regular one.
            segment = segment.Replace("%2a", "*").Replace("%2A", "*");
            UriBuilder ub = new UriBuilder(s_dummyUri);
            ub.Path = segment;
            string escapedIfNecessarySegment = ub.Uri.AbsolutePath.Substring(1);
            if (escapedIfNecessarySegment == string.Empty)
            {
                // This path through UriBuilder will sometimes '----' various segments
                // such as '../' and './'.  When this happens and the result is an empty
                // string, we should just throw and tell the user we don't handle that.
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(segment),
                    SR.Format(SR.UTInvalidFormatSegmentOrQueryPart, segment));
            }

            return new UriTemplateLiteralPathSegment(escapedIfNecessarySegment);
        }

        public static UriTemplateLiteralPathSegment CreateFromWireData(string segment)
        {
            return new UriTemplateLiteralPathSegment(segment);
        }

        public string AsUnescapedString()
        {
            Fx.Assert(_segment != null, "this should only be called by Bind\\Lookup");
            return Uri.UnescapeDataString(_segment);
        }

        public override void Bind(string[] values, ref int valueIndex, StringBuilder path)
        {
            if (EndsWithSlash)
            {
                path.AppendFormat("{0}/", AsUnescapedString());
            }
            else
            {
                path.Append(AsUnescapedString());
            }
        }

        public int CompareTo(UriTemplateLiteralPathSegment other) => StringComparer.OrdinalIgnoreCase.Compare(_segment, other._segment);

        public override bool Equals(object obj)
        {
            if (!(obj is UriTemplateLiteralPathSegment lps))
            {
                Fx.Assert("why would we ever call this?");
                return false;
            }
            else
            {
                return ((EndsWithSlash == lps.EndsWithSlash) &&
                    StringComparer.OrdinalIgnoreCase.Equals(_segment, lps._segment));
            }
        }

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_segment);

        public override bool IsEquivalentTo(UriTemplatePathSegment other, bool ignoreTrailingSlash)
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

            UriTemplateLiteralPathSegment otherAsLiteral = other as UriTemplateLiteralPathSegment;
            Fx.Assert(otherAsLiteral != null, "The nature requires that this will be OK");

            return IsMatch(otherAsLiteral, ignoreTrailingSlash);
        }

        public override bool IsMatch(UriTemplateLiteralPathSegment segment, bool ignoreTrailingSlash)
        {
            if (!ignoreTrailingSlash && (segment.EndsWithSlash != this.EndsWithSlash))
            {
                return false;
            }

            return CompareTo(segment) == 0;
        }

        public bool IsNullOrEmpty() => string.IsNullOrEmpty(_segment);

        public override void Lookup(string segment, NameValueCollection boundParameters)
        {
            Fx.Assert(StringComparer.OrdinalIgnoreCase.Compare(AsUnescapedString(), segment) == 0,
                "How can that be? Lookup is expected to be called after IsMatch");
        }
    }
}
