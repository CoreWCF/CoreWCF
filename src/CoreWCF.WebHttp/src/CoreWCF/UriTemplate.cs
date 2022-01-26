// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class UriTemplate
    {
        internal readonly int _firstOptionalSegment;
        internal readonly string _originalTemplate;
        internal readonly Dictionary<string, UriTemplateQueryValue> _queries; // keys are original case specified in UriTemplate constructor, dictionary ignores case
        internal readonly List<UriTemplatePathSegment> _segments;

        internal const string WildcardPath = "*";

        private readonly Dictionary<string, string> _additionalDefaults; // keys are original case specified in UriTemplate constructor, dictionary ignores case
        private readonly string _fragment;
        private const string NullableDefault = "null";
        private readonly WildcardInfo _wildcard;
        private IDictionary<string, string> _defaults;
        private ConcurrentDictionary<string, string> _unescapedDefaults;

        private VariablesCollection _variables;

        // constructors validates that template is well-formed
        public UriTemplate(string template)
            : this(template, false)
        {
        }

        public UriTemplate(string template, bool ignoreTrailingSlash)
            : this(template, ignoreTrailingSlash, null)
        {
        }

        public UriTemplate(string template, IDictionary<string, string> additionalDefaults)
            : this(template, false, additionalDefaults)
        {
        }

        public UriTemplate(string template, bool ignoreTrailingSlash, IDictionary<string, string> additionalDefaults)
        {
            _originalTemplate = template ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(template));

            IgnoreTrailingSlash = ignoreTrailingSlash;
            _segments = new List<UriTemplatePathSegment>();
            _queries = new Dictionary<string, UriTemplateQueryValue>(StringComparer.OrdinalIgnoreCase);

            // parse it
            string pathTemplate;
            string queryTemplate;
            // ignore a leading slash
            if (template.StartsWith("/", StringComparison.Ordinal))
            {
                template = template.Substring(1);
            }

            // pull out fragment
            int fragmentStart = template.IndexOf('#');
            if (fragmentStart == -1)
            {
                _fragment = "";
            }
            else
            {
                _fragment = template.Substring(fragmentStart + 1);
                template = template.Substring(0, fragmentStart);
            }

            // pull out path and query
            int queryStart = template.IndexOf('?');
            if (queryStart == -1)
            {
                queryTemplate = string.Empty;
                pathTemplate = template;
            }
            else
            {
                queryTemplate = template.Substring(queryStart + 1);
                pathTemplate = template.Substring(0, queryStart);
            }

            template = null; // to ensure we don't accidentally reference this variable any more

            // setup path template and validate
            if (!string.IsNullOrEmpty(pathTemplate))
            {
                int startIndex = 0;
                while (startIndex < pathTemplate.Length)
                {
                    // Identify the next segment
                    int endIndex = pathTemplate.IndexOf('/', startIndex);
                    string segment;
                    if (endIndex != -1)
                    {
                        segment = pathTemplate.Substring(startIndex, endIndex + 1 - startIndex);
                        startIndex = endIndex + 1;
                    }
                    else
                    {
                        segment = pathTemplate.Substring(startIndex);
                        startIndex = pathTemplate.Length;
                    }

                    // Checking for wildcard segment ("*") or ("{*<var name>}")
                    if ((startIndex == pathTemplate.Length) &&
                        UriTemplateHelpers.IsWildcardSegment(segment, out UriTemplatePartType wildcardType))
                    {
                        switch (wildcardType)
                        {
                            case UriTemplatePartType.Literal:
                                _wildcard = new WildcardInfo(this);
                                break;

                            case UriTemplatePartType.Variable:
                                _wildcard = new WildcardInfo(this, segment);
                                break;

                            default:
                                Fx.Assert("Error in identifying the type of the wildcard segment");
                                break;
                        }
                    }
                    else
                    {
                        _segments.Add(UriTemplatePathSegment.CreateFromUriTemplate(segment, this));
                    }
                }
            }

            // setup query template and validate
            if (!string.IsNullOrEmpty(queryTemplate))
            {
                int startIndex = 0;
                while (startIndex < queryTemplate.Length)
                {
                    // Identify the next query part
                    int endIndex = queryTemplate.IndexOf('&', startIndex);
                    int queryPartStart = startIndex;
                    int queryPartEnd;
                    if (endIndex != -1)
                    {
                        queryPartEnd = endIndex;
                        startIndex = endIndex + 1;
                        if (startIndex >= queryTemplate.Length)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                                SR.UTQueryCannotEndInAmpersand, _originalTemplate)));
                        }
                    }
                    else
                    {
                        queryPartEnd = queryTemplate.Length;
                        startIndex = queryTemplate.Length;
                    }

                    // Checking query part type; identifying key and value
                    int equalSignIndex = queryTemplate.IndexOf('=', queryPartStart, queryPartEnd - queryPartStart);
                    string key;
                    string value;
                    if (equalSignIndex >= 0)
                    {
                        key = queryTemplate.Substring(queryPartStart, equalSignIndex - queryPartStart);
                        value = queryTemplate.Substring(equalSignIndex + 1, queryPartEnd - equalSignIndex - 1);
                    }
                    else
                    {
                        key = queryTemplate.Substring(queryPartStart, queryPartEnd - queryPartStart);
                        value = null;
                    }

                    if (string.IsNullOrEmpty(key))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                            SR.UTQueryCannotHaveEmptyName, _originalTemplate)));
                    }

                    if (UriTemplateHelpers.IdentifyPartType(key) != UriTemplatePartType.Literal)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(template), SR.Format(
                            SR.UTQueryMustHaveLiteralNames, _originalTemplate));
                    }

                    // Adding a new entry to the queries dictionary
                    key = UrlUtility.UrlDecode(key, Encoding.UTF8);
                    if (_queries.ContainsKey(key))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                            SR.UTQueryNamesMustBeUnique, _originalTemplate)));
                    }

                    _queries.Add(key, UriTemplateQueryValue.CreateFromUriTemplate(value, this));
                }
            }

            // Process additional defaults (if has some) :
            if (additionalDefaults != null)
            {
                if (_variables == null)
                {
                    if (additionalDefaults.Count > 0)
                    {
                        _additionalDefaults = new Dictionary<string, string>(additionalDefaults, StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in additionalDefaults)
                    {
                        string uppercaseKey = kvp.Key.ToUpperInvariant();
                        if ((_variables.DefaultValues != null) && _variables.DefaultValues.ContainsKey(uppercaseKey))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(additionalDefaults),
                                SR.Format(SR.UTAdditionalDefaultIsInvalid, kvp.Key, _originalTemplate));
                        }

                        if (_variables.PathSegmentVariableNames.Contains(uppercaseKey))
                        {
                            _variables.AddDefaultValue(uppercaseKey, kvp.Value);
                        }
                        else if (_variables.QueryValueVariableNames.Contains(uppercaseKey))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.UTDefaultValueToQueryVarFromAdditionalDefaults, _originalTemplate,
                                uppercaseKey)));
                        }
                        else if (string.Compare(kvp.Value, UriTemplate.NullableDefault, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.UTNullableDefaultAtAdditionalDefaults, _originalTemplate,
                                uppercaseKey)));
                        }
                        else
                        {
                            if (_additionalDefaults == null)
                            {
                                _additionalDefaults = new Dictionary<string, string>(additionalDefaults.Count, StringComparer.OrdinalIgnoreCase);
                            }

                            _additionalDefaults.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }

            // Validate defaults (if should)
            if ((_variables != null) && (_variables.DefaultValues != null))
            {
                _variables.ValidateDefaults(out _firstOptionalSegment);
            }
            else
            {
                _firstOptionalSegment = _segments.Count;
            }
        }

        public IDictionary<string, string> Defaults
        {
            get
            {
                if (_defaults == null)
                {
                    Interlocked.CompareExchange(ref _defaults, new UriTemplateDefaults(this), null);
                }

                return _defaults;
            }
        }

        public bool IgnoreTrailingSlash { get; }

        public ReadOnlyCollection<string> PathSegmentVariableNames
        {
            get
            {
                if (_variables == null)
                {
                    return VariablesCollection.EmptyCollection;
                }
                else
                {
                    return _variables.PathSegmentVariableNames;
                }
            }
        }

        public ReadOnlyCollection<string> QueryValueVariableNames
        {
            get
            {
                if (_variables == null)
                {
                    return VariablesCollection.EmptyCollection;
                }
                else
                {
                    return _variables.QueryValueVariableNames;
                }
            }
        }

        internal bool HasNoVariables
        {
            get
            {
                return _variables == null;
            }
        }

        internal bool HasWildcard
        {
            get
            {
                return (_wildcard != null);
            }
        }

        // make a Uri by subbing in the values, throw on bad input
        public Uri BindByName(Uri baseAddress, IDictionary<string, string> parameters)
        {
            return BindByName(baseAddress, parameters, false);
        }

        public Uri BindByName(Uri baseAddress, IDictionary<string, string> parameters, bool omitDefaults)
        {
            if (baseAddress == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(baseAddress));
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.Format(
                    SR.UTBadBaseAddress));
            }

            BindInformation bindInfo;
            if (_variables == null)
            {
                bindInfo = PrepareBindInformation(parameters, omitDefaults);
            }
            else
            {
                bindInfo = _variables.PrepareBindInformation(parameters, omitDefaults);
            }

            return Bind(baseAddress, bindInfo, omitDefaults);
        }

        public Uri BindByName(Uri baseAddress, NameValueCollection parameters)
        {
            return BindByName(baseAddress, parameters, false);
        }

        public Uri BindByName(Uri baseAddress, NameValueCollection parameters, bool omitDefaults)
        {
            if (baseAddress == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(baseAddress));
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.Format(
                    SR.UTBadBaseAddress));
            }

            BindInformation bindInfo;
            if (_variables == null)
            {
                bindInfo = PrepareBindInformation(parameters, omitDefaults);
            }
            else
            {
                bindInfo = _variables.PrepareBindInformation(parameters, omitDefaults);
            }

            return Bind(baseAddress, bindInfo, omitDefaults);
        }

        public Uri BindByPosition(Uri baseAddress, params string[] values)
        {
            if (baseAddress == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(baseAddress));
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.Format(
                    SR.UTBadBaseAddress));
            }

            BindInformation bindInfo;
            if (_variables == null)
            {
                if (values.Length > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(
                        SR.UTBindByPositionNoVariables, _originalTemplate, values.Length)));
                }
                bindInfo = new BindInformation(_additionalDefaults);
            }
            else
            {
                bindInfo = _variables.PrepareBindInformation(values);
            }

            return Bind(baseAddress, bindInfo, false);
        }

        // A note about UriTemplate equivalency:
        //  The introduction of defaults and, more over, terminal defaults, broke the simple
        //  intuitive notion of equivalency between templates. We will define equivalent
        //  templates as such based on the structure of them and not based on the set of uri
        //  that are matched by them. The result is that, even though they do not match the
        //  same set of uri's, the following templates are equivalent:
        //      - "/foo/{bar}"
        //      - "/foo/{bar=xyz}"
        //  A direct result from the support for 'terminal defaults' is that the IsPathEquivalentTo
        //  method, which was used both to determine the equivalence between templates, as 
        //  well as verify that all the templates, combined together in the same PathEquivalentSet, 
        //  are equivalent in their path is no longer valid for both purposes. We will break 
        //  it to two distinct methods, each will be called in a different case.
        public bool IsEquivalentTo(UriTemplate other)
        {
            if (other == null)
            {
                return false;
            }

            if (other._segments == null || other._queries == null)
            {
                // they never are null, but PreSharp is complaining, 
                // and warning suppression isn't working
                return false;
            }

            if (!IsPathFullyEquivalent(other))
            {
                return false;
            }
            
            if (!IsQueryEquivalent(other))
            {
                return false;
            }

            Fx.Assert(UriTemplateEquivalenceComparer.Instance.GetHashCode(this) == UriTemplateEquivalenceComparer.Instance.GetHashCode(other), "bad GetHashCode impl");
            return true;
        }

        public UriTemplateMatch Match(Uri baseAddress, Uri candidate)
        {
            if (baseAddress == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(baseAddress));
            }

            if (!baseAddress.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.Format(
                    SR.UTBadBaseAddress));
            }

            if (candidate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(candidate));
            }

            // ensure that the candidate is 'under' the base address
            if (!candidate.IsAbsoluteUri)
            {
                return null;
            }

            string basePath = UriTemplateHelpers.GetUriPath(baseAddress);
            string candidatePath = UriTemplateHelpers.GetUriPath(candidate);
            if (candidatePath.Length < basePath.Length)
            {
                return null;
            }

            if (!candidatePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Identifying the relative segments \ checking matching to the path :
            int numSegmentsInBaseAddress = baseAddress.Segments.Length;
            string[] candidateSegments = candidate.Segments;
            if (!IsCandidatePathMatch(numSegmentsInBaseAddress, candidateSegments,
                out int numMatchedSegments, out Collection<string> relativeCandidateSegments))
            {
                return null;
            }

            // Checking matching to the query (if should) :
            NameValueCollection candidateQuery = null;
            if (!UriTemplateHelpers.CanMatchQueryTrivially(this))
            {
                candidateQuery = UriTemplateHelpers.ParseQueryString(candidate.Query);
                if (!UriTemplateHelpers.CanMatchQueryInterestingly(this, candidateQuery, false))
                {
                    return null;
                }
            }

            // We matched; lets build the UriTemplateMatch
            return CreateUriTemplateMatch(baseAddress, candidate, null, numMatchedSegments,
                relativeCandidateSegments, candidateQuery);
        }

        public override string ToString() => _originalTemplate;

        internal string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration)
        {
            return AddPathVariable(sourceNature, varDeclaration, out _);
        }

        internal string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration,
            out bool hasDefaultValue)
        {
            if (_variables == null)
            {
                _variables = new VariablesCollection(this);
            }

            return _variables.AddPathVariable(sourceNature, varDeclaration, out hasDefaultValue);
        }

        internal string AddQueryVariable(string varDeclaration)
        {
            if (_variables == null)
            {
                _variables = new VariablesCollection(this);
            }

            return _variables.AddQueryVariable(varDeclaration);
        }

        internal UriTemplateMatch CreateUriTemplateMatch(Uri baseUri, Uri uri, object data,
            int numMatchedSegments, Collection<string> relativePathSegments, NameValueCollection uriQuery)
        {
            UriTemplateMatch result = new UriTemplateMatch
            {
                RequestUri = uri,
                BaseUri = baseUri
            };

            if (uriQuery != null)
            {
                result.SetQueryParameters(uriQuery);
            }

            result.SetRelativePathSegments(relativePathSegments);
            result.Data = data;
            result.Template = this;
            for (int i = 0; i < numMatchedSegments; i++)
            {
                _segments[i].Lookup(result.RelativePathSegments[i], result.BoundVariables);
            }

            if (_wildcard != null)
            {
                _wildcard.Lookup(numMatchedSegments, result.RelativePathSegments,
                    result.BoundVariables);
            }
            else if (numMatchedSegments < _segments.Count)
            {
                BindTerminalDefaults(numMatchedSegments, result.BoundVariables);
            }

            if (_queries.Count > 0)
            {
                foreach (KeyValuePair<string, UriTemplateQueryValue> kvp in _queries)
                {
                    kvp.Value.Lookup(result.QueryParameters[kvp.Key], result.BoundVariables);
                    //UriTemplateHelpers.AssertCanonical(varName);
                }
            }

            if (_additionalDefaults != null)
            {
                foreach (KeyValuePair<string, string> kvp in _additionalDefaults)
                {
                    result.BoundVariables.Add(kvp.Key, UnescapeDefaultValue(kvp.Value));
                }
            }

            Fx.Assert(result.RelativePathSegments.Count - numMatchedSegments >= 0, "bad segment computation");
            result.SetWildcardPathSegmentsStart(numMatchedSegments);

            return result;
        }

        internal bool IsPathPartiallyEquivalentAt(UriTemplate other, int segmentsCount)
        {
            // Refer to the note on template equivalency at IsEquivalentTo
            // This method checks if any uri with given number of segments, which can be matched
            //  by this template, can be also matched by the other template.
            Fx.Assert(segmentsCount >= _firstOptionalSegment - 1, "How can that be? The Trie is constructed that way!");
            Fx.Assert(segmentsCount <= _segments.Count, "How can that be? The Trie is constructed that way!");
            Fx.Assert(segmentsCount >= other._firstOptionalSegment - 1, "How can that be? The Trie is constructed that way!");
            Fx.Assert(segmentsCount <= other._segments.Count, "How can that be? The Trie is constructed that way!");

            for (int i = 0; i < segmentsCount; ++i)
            {
                if (!_segments[i].IsEquivalentTo(other._segments[i],
                    ((i == segmentsCount - 1) && (IgnoreTrailingSlash || other.IgnoreTrailingSlash))))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool IsQueryEquivalent(UriTemplate other)
        {
            if (_queries.Count != other._queries.Count)
            {
                return false;
            }

            foreach (string key in _queries.Keys)
            {
                UriTemplateQueryValue utqv = _queries[key];
                if (!other._queries.TryGetValue(key, out UriTemplateQueryValue otherUtqv))
                {
                    return false;
                }

                if (!utqv.IsEquivalentTo(otherUtqv))
                {
                    return false;
                }
            }

            return true;
        }

        internal static Uri RewriteUri(Uri uri, string host)
        {
            if (!string.IsNullOrEmpty(host))
            {
                string originalHostHeader = uri.Host + ((!uri.IsDefaultPort) ? ":" + uri.Port.ToString(CultureInfo.InvariantCulture) : string.Empty);
                if (!string.Equals(originalHostHeader, host, StringComparison.OrdinalIgnoreCase))
                {
                    var sourceUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}://{1}", uri.Scheme, host));
                    return (new UriBuilder(uri) { Host = sourceUri.Host, Port = sourceUri.Port }).Uri;
                }
            }

            return uri;
        }

        private Uri Bind(Uri baseAddress, BindInformation bindInfo, bool omitDefaults)
        {
            UriBuilder result = new UriBuilder(baseAddress);
            int parameterIndex = 0;
            int lastPathParameter = ((_variables == null) ? -1 : _variables.PathSegmentVariableNames.Count - 1);
            int lastPathParameterToBind;
            if (lastPathParameter == -1)
            {
                lastPathParameterToBind = -1;
            }
            else if (omitDefaults)
            {
                lastPathParameterToBind = bindInfo.LastNonDefaultPathParameter;
            }
            else
            {
                lastPathParameterToBind = bindInfo.LastNonNullablePathParameter;
            }

            string[] parameters = bindInfo.NormalizedParameters;
            IDictionary<string, string> extraQueryParameters = bindInfo.AdditionalParameters;
            // Binding the path :
            StringBuilder pathString = new StringBuilder(result.Path);
            if (pathString[pathString.Length - 1] != '/')
            {
                pathString.Append('/');
            }

            if (lastPathParameterToBind < lastPathParameter)
            {
                // Binding all the parameters we need
                int segmentIndex = 0;
                while (parameterIndex <= lastPathParameterToBind)
                {
                    Fx.Assert(segmentIndex < _segments.Count,
                        "Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
                    _segments[segmentIndex++].Bind(parameters, ref parameterIndex, pathString);
                }
                Fx.Assert(parameterIndex == lastPathParameterToBind + 1,
                    "That is the exit criteria from the loop");
                // Maybe we have some literals yet to bind
                Fx.Assert(segmentIndex < _segments.Count,
                    "Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
                while (_segments[segmentIndex].Nature == UriTemplatePartType.Literal)
                {
                    _segments[segmentIndex++].Bind(parameters, ref parameterIndex, pathString);
                    Fx.Assert(parameterIndex == lastPathParameterToBind + 1,
                        "We have moved the parameter index in a literal binding");
                    Fx.Assert(segmentIndex < _segments.Count,
                        "Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
                }
                // We're done; skip to the beggining of the query parameters
                parameterIndex = lastPathParameter + 1;
            }
            else if (_segments.Count > 0 || _wildcard != null)
            {
                for (int i = 0; i < _segments.Count; i++)
                {
                    _segments[i].Bind(parameters, ref parameterIndex, pathString);
                }

                if (_wildcard != null)
                {
                    _wildcard.Bind(parameters, ref parameterIndex, pathString);
                }
            }

            if (IgnoreTrailingSlash && (pathString[pathString.Length - 1] == '/'))
            {
                pathString.Remove(pathString.Length - 1, 1);
            }

            result.Path = pathString.ToString();
            // Binding the query :
            if ((_queries.Count != 0) || (extraQueryParameters != null))
            {
                StringBuilder query = new StringBuilder("");
                foreach (string key in _queries.Keys)
                {
                    _queries[key].Bind(key, parameters, ref parameterIndex, query);
                }

                if (extraQueryParameters != null)
                {
                    foreach (string key in extraQueryParameters.Keys)
                    {
                        if (_queries.ContainsKey(key.ToUpperInvariant()))
                        {
                            // This can only be if the key passed has the same name as some literal key
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(parameters), SR.Format(
                                SR.UTBothLiteralAndNameValueCollectionKey, key));
                        }
                        string value = extraQueryParameters[key];
                        string escapedValue = (string.IsNullOrEmpty(value) ? string.Empty : UrlUtility.UrlEncode(value, Encoding.UTF8));
                        query.AppendFormat("&{0}={1}", UrlUtility.UrlEncode(key, Encoding.UTF8), escapedValue);
                    }
                }

                if (query.Length != 0)
                {
                    query.Remove(0, 1); // remove extra leading '&'
                }

                result.Query = query.ToString();
            }

            // Adding the fragment (if needed)
            if (_fragment != null)
            {
                result.Fragment = _fragment;
            }

            return result.Uri;
        }

        private void BindTerminalDefaults(int numMatchedSegments, NameValueCollection boundParameters)
        {
            Fx.Assert(!HasWildcard, "There are no terminal default when ends with wildcard");
            Fx.Assert(numMatchedSegments < _segments.Count, "Otherwise - no defaults to bind");
            Fx.Assert(_variables != null, "Otherwise - no default values to bind");
            Fx.Assert(_variables.DefaultValues != null, "Otherwise - no default values to bind");

            for (int i = numMatchedSegments; i < _segments.Count; i++)
            {
                switch (_segments[i].Nature)
                {
                    case UriTemplatePartType.Variable:
                        {
                            UriTemplateVariablePathSegment vps = _segments[i] as UriTemplateVariablePathSegment;
                            Fx.Assert(vps != null, "How can that be? That its nature");
                            _variables.LookupDefault(vps.VarName, boundParameters);
                        }
                        break;

                    default:
                        Fx.Assert("We only support terminal defaults on Variable segments");
                        break;
                }
            }
        }

        private bool IsCandidatePathMatch(int numSegmentsInBaseAddress, string[] candidateSegments,
            out int numMatchedSegments, out Collection<string> relativeSegments)
        {
            int numRelativeSegments = candidateSegments.Length - numSegmentsInBaseAddress;
            Fx.Assert(numRelativeSegments >= 0, "bad segments num");
            relativeSegments = new Collection<string>();
            bool isStillMatch = true;
            int relativeSegmentsIndex = 0;

            while (isStillMatch && (relativeSegmentsIndex < numRelativeSegments))
            {
                string segment = candidateSegments[relativeSegmentsIndex + numSegmentsInBaseAddress];
                // Mathcing to next regular segment in the template (if there is one); building the wire segment representation
                if (relativeSegmentsIndex < _segments.Count)
                {
                    bool ignoreSlash = (this.IgnoreTrailingSlash && (relativeSegmentsIndex == numRelativeSegments - 1));
                    UriTemplateLiteralPathSegment lps = UriTemplateLiteralPathSegment.CreateFromWireData(segment);
                    if (!_segments[relativeSegmentsIndex].IsMatch(lps, ignoreSlash))
                    {
                        isStillMatch = false;
                        break;
                    }

                    string relPathSeg = Uri.UnescapeDataString(segment);
                    if (lps.EndsWithSlash)
                    {
                        Fx.Assert(relPathSeg.EndsWith("/", StringComparison.Ordinal), "problem with relative path segment");
                        relPathSeg = relPathSeg.Substring(0, relPathSeg.Length - 1); // trim slash
                    }

                    relativeSegments.Add(relPathSeg);
                }
                // Checking if the template has a wild card ('*') or a final star var segment ("{*<var name>}"
                else if (HasWildcard)
                {
                    break;
                }
                else
                {
                    isStillMatch = false;
                    break;
                }

                relativeSegmentsIndex++;
            }

            if (isStillMatch)
            {
                numMatchedSegments = relativeSegmentsIndex;
                // building the wire representation to segments that were matched to a wild card
                if (relativeSegmentsIndex < numRelativeSegments)
                {
                    while (relativeSegmentsIndex < numRelativeSegments)
                    {
                        string relPathSeg = Uri.UnescapeDataString(candidateSegments[relativeSegmentsIndex + numSegmentsInBaseAddress]);
                        if (relPathSeg.EndsWith("/", StringComparison.Ordinal))
                        {
                            relPathSeg = relPathSeg.Substring(0, relPathSeg.Length - 1); // trim slash
                        }
                        relativeSegments.Add(relPathSeg);
                        relativeSegmentsIndex++;
                    }
                }
                // Checking if we matched all required segments already
                else if (numMatchedSegments < _firstOptionalSegment)
                {
                    isStillMatch = false;
                }
            }
            else
            {
                numMatchedSegments = 0;
            }

            return isStillMatch;
        }

        private bool IsPathFullyEquivalent(UriTemplate other)
        {
            // Refer to the note on template equivalency at IsEquivalentTo
            // This method checks if both templates has a fully equivalent path.
            if (HasWildcard != other.HasWildcard)
            {
                return false;
            }

            if (_segments.Count != other._segments.Count)
            {
                return false;
            }

            for (int i = 0; i < _segments.Count; ++i)
            {
                if (!_segments[i].IsEquivalentTo(other._segments[i],
                    (i == _segments.Count - 1) && !HasWildcard && (IgnoreTrailingSlash || other.IgnoreTrailingSlash)))
                {
                    return false;
                }
            }

            return true;
        }

        private BindInformation PrepareBindInformation(IDictionary<string, string> parameters, bool omitDefaults)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            IDictionary<string, string> extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
            foreach (KeyValuePair<string, string> kvp in parameters)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(parameters),
                        SR.Format(SR.UTBindByNameCalledWithEmptyKey));
                }

                extraParameters.Add(kvp);
            }

            ProcessDefaultsAndCreateBindInfo(omitDefaults, extraParameters, out BindInformation bindInfo);

            return bindInfo;
        }

        private BindInformation PrepareBindInformation(NameValueCollection parameters, bool omitDefaults)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            IDictionary<string, string> extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
            foreach (string key in parameters.AllKeys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(parameters),
                        SR.Format(SR.UTBindByNameCalledWithEmptyKey));
                }

                extraParameters.Add(key, parameters[key]);
            }

            ProcessDefaultsAndCreateBindInfo(omitDefaults, extraParameters, out BindInformation bindInfo);

            return bindInfo;
        }

        private void ProcessDefaultsAndCreateBindInfo(bool omitDefaults, IDictionary<string, string> extraParameters,
            out BindInformation bindInfo)
        {
            Fx.Assert(extraParameters != null, "We are expected to create it at the calling PrepareBindInformation");

            if (_additionalDefaults != null)
            {
                if (omitDefaults)
                {
                    foreach (KeyValuePair<string, string> kvp in _additionalDefaults)
                    {
                        if (extraParameters.TryGetValue(kvp.Key, out string extraParameter))
                        {
                            if (string.Compare(extraParameter, kvp.Value, StringComparison.Ordinal) == 0)
                            {
                                extraParameters.Remove(kvp.Key);
                            }
                        }
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in _additionalDefaults)
                    {
                        if (!extraParameters.ContainsKey(kvp.Key))
                        {
                            extraParameters.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }

            if (extraParameters.Count == 0)
            {
                extraParameters = null;
            }

            bindInfo = new BindInformation(extraParameters);
        }

        private string UnescapeDefaultValue(string escapedValue)
        {
            if (string.IsNullOrEmpty(escapedValue))
            {
                return escapedValue;
            }

            if (_unescapedDefaults == null)
            {
                _unescapedDefaults = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            }

            return _unescapedDefaults.GetOrAdd(escapedValue, Uri.UnescapeDataString);
        }

        internal struct BindInformation
        {
            public BindInformation(string[] normalizedParameters, int lastNonDefaultPathParameter,
                int lastNonNullablePathParameter, IDictionary<string, string> additionalParameters)
            {
                NormalizedParameters = normalizedParameters;
                LastNonDefaultPathParameter = lastNonDefaultPathParameter;
                LastNonNullablePathParameter = lastNonNullablePathParameter;
                AdditionalParameters = additionalParameters;
            }

            public BindInformation(IDictionary<string, string> additionalParameters)
            {
                NormalizedParameters = null;
                LastNonDefaultPathParameter = -1;
                LastNonNullablePathParameter = -1;
                AdditionalParameters = additionalParameters;
            }

            public IDictionary<string, string> AdditionalParameters { get; }
            public int LastNonDefaultPathParameter { get; }
            public int LastNonNullablePathParameter { get; }
            public string[] NormalizedParameters { get; }
        }

        internal class UriTemplateDefaults : IDictionary<string, string>
        {
            private readonly Dictionary<string, string> _defaults;
            private readonly ReadOnlyCollection<string> _keys;
            private readonly ReadOnlyCollection<string> _values;

            public UriTemplateDefaults(UriTemplate template)
            {
                _defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if ((template._variables != null) && (template._variables.DefaultValues != null))
                {
                    foreach (KeyValuePair<string, string> kvp in template._variables.DefaultValues)
                    {
                        _defaults.Add(kvp.Key, kvp.Value);
                    }
                }

                if (template._additionalDefaults != null)
                {
                    foreach (KeyValuePair<string, string> kvp in template._additionalDefaults)
                    {
                        _defaults.Add(kvp.Key.ToUpperInvariant(), kvp.Value);
                    }
                }

                _keys = new ReadOnlyCollection<string>(new List<string>(_defaults.Keys));
                _values = new ReadOnlyCollection<string>(new List<string>(_defaults.Values));
            }

            public int Count => _defaults.Count;

            public bool IsReadOnly => true;

            public ICollection<string> Keys => _keys;

            public ICollection<string> Values => _values;

            public string this[string key]
            {
                get
                {
                    return _defaults[key];
                }
                set
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                        SR.Format(SR.UTDefaultValuesAreImmutable)));
                }
            }

            public void Add(string key, string value)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                    SR.Format(SR.UTDefaultValuesAreImmutable)));
            }

            public void Add(KeyValuePair<string, string> item)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                    SR.Format(SR.UTDefaultValuesAreImmutable)));
            }

            public void Clear()
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                    SR.Format(SR.UTDefaultValuesAreImmutable)));
            }

            public bool Contains(KeyValuePair<string, string> item)
            {
                return (_defaults as ICollection<KeyValuePair<string, string>>).Contains(item);
            }

            public bool ContainsKey(string key)
            {
                return _defaults.ContainsKey(key);
            }

            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
            {
                (_defaults as ICollection<KeyValuePair<string, string>>).CopyTo(array, arrayIndex);
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return _defaults.GetEnumerator();
            }

            public bool Remove(string key)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                    SR.Format(SR.UTDefaultValuesAreImmutable)));
            }

            public bool Remove(KeyValuePair<string, string> item)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                    SR.Format(SR.UTDefaultValuesAreImmutable)));
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _defaults.GetEnumerator();
            }

            public bool TryGetValue(string key, out string value)
            {
                return _defaults.TryGetValue(key, out value);
            }
        }

        internal class VariablesCollection
        {
            private readonly UriTemplate _owner;
            private static ReadOnlyCollection<string> s_emptyStringCollection = null;
            private int _firstNullablePathVariable;
            private readonly List<string> _pathSegmentVariableNames; // ToUpperInvariant, in order they occur in the original template string
            private ReadOnlyCollection<string> _pathSegmentVariableNamesSnapshot = null;
            private readonly List<UriTemplatePartType> _pathSegmentVariableNature;
            private List<string> _queryValueVariableNames; // ToUpperInvariant, in order they occur in the original template string
            private ReadOnlyCollection<string> _queryValueVariableNamesSnapshot = null;

            public VariablesCollection(UriTemplate owner)
            {
                _owner = owner;
                _pathSegmentVariableNames = new List<string>();
                _pathSegmentVariableNature = new List<UriTemplatePartType>();
                _queryValueVariableNames = new List<string>();
                _firstNullablePathVariable = -1;
            }

            public static ReadOnlyCollection<string> EmptyCollection
            {
                get
                {
                    if (s_emptyStringCollection == null)
                    {
                        s_emptyStringCollection = new ReadOnlyCollection<string>(new List<string>());
                    }

                    return s_emptyStringCollection;
                }
            }

            public Dictionary<string, string> DefaultValues { get; private set; }

            public ReadOnlyCollection<string> PathSegmentVariableNames
            {
                get
                {
                    if (_pathSegmentVariableNamesSnapshot == null)
                    {
                        Interlocked.CompareExchange(ref _pathSegmentVariableNamesSnapshot, new ReadOnlyCollection<string>(
                            _pathSegmentVariableNames), null);
                    }

                    return _pathSegmentVariableNamesSnapshot;
                }
            }

            public ReadOnlyCollection<string> QueryValueVariableNames
            {
                get
                {
                    if (_queryValueVariableNamesSnapshot == null)
                    {
                        Interlocked.CompareExchange(ref _queryValueVariableNamesSnapshot, new ReadOnlyCollection<string>(
                            _queryValueVariableNames), null);
                    }

                    return _queryValueVariableNamesSnapshot;
                }
            }

            public void AddDefaultValue(string varName, string value)
            {
                int varIndex = _pathSegmentVariableNames.IndexOf(varName);
                Fx.Assert(varIndex != -1, "Adding default value is restricted to path variables");
                if ((_owner._wildcard != null) && _owner._wildcard.HasVariable &&
                    (varIndex == _pathSegmentVariableNames.Count - 1))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UTStarVariableWithDefaultsFromAdditionalDefaults,
                        _owner._originalTemplate, varName)));
                }

                if (_pathSegmentVariableNature[varIndex] != UriTemplatePartType.Variable)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UTDefaultValueToCompoundSegmentVarFromAdditionalDefaults,
                        _owner._originalTemplate, varName)));
                }

                if (string.IsNullOrEmpty(value) ||
                    (string.Compare(value, NullableDefault, StringComparison.OrdinalIgnoreCase) == 0))
                {
                    value = null;
                }

                if (DefaultValues == null)
                {
                    DefaultValues = new Dictionary<string, string>();
                }

                DefaultValues.Add(varName, value);
            }

            public string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration, out bool hasDefaultValue)
            {
                Fx.Assert(sourceNature != UriTemplatePartType.Literal, "Literal path segments can't be the source for path variables");

                ParseVariableDeclaration(varDeclaration, out string varName, out string defaultValue);
                hasDefaultValue = (defaultValue != null);
                if (varName.IndexOf(WildcardPath, StringComparison.Ordinal) != -1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                        SR.Format(SR.UTInvalidWildcardInVariableOrLiteral, _owner._originalTemplate, WildcardPath)));
                }

                string uppercaseVarName = varName.ToUpperInvariant();
                if (_pathSegmentVariableNames.Contains(uppercaseVarName) ||
                    _queryValueVariableNames.Contains(uppercaseVarName))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UTVarNamesMustBeUnique, _owner._originalTemplate, varName)));
                }

                _pathSegmentVariableNames.Add(uppercaseVarName);
                _pathSegmentVariableNature.Add(sourceNature);
                if (hasDefaultValue)
                {
                    if (defaultValue == string.Empty)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                            SR.Format(SR.UTInvalidDefaultPathValue, _owner._originalTemplate,
                            varDeclaration, varName)));
                    }

                    if (string.Compare(defaultValue, NullableDefault, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        defaultValue = null;
                    }

                    if (DefaultValues == null)
                    {
                        DefaultValues = new Dictionary<string, string>();
                    }

                    DefaultValues.Add(uppercaseVarName, defaultValue);
                }

                return uppercaseVarName;
            }

            public string AddQueryVariable(string varDeclaration)
            {
                ParseVariableDeclaration(varDeclaration, out string varName, out string defaultValue);
                if (varName.IndexOf(WildcardPath, StringComparison.Ordinal) != -1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                        SR.Format(SR.UTInvalidWildcardInVariableOrLiteral, _owner._originalTemplate, WildcardPath)));
                }

                if (defaultValue != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UTDefaultValueToQueryVar, _owner._originalTemplate,
                        varDeclaration, varName)));
                }

                string uppercaseVarName = varName.ToUpperInvariant();
                if (_pathSegmentVariableNames.Contains(uppercaseVarName) ||
                    _queryValueVariableNames.Contains(uppercaseVarName))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.UTVarNamesMustBeUnique, _owner._originalTemplate, varName)));
                }
                _queryValueVariableNames.Add(uppercaseVarName);

                return uppercaseVarName;
            }

            public void LookupDefault(string varName, NameValueCollection boundParameters)
            {
                Fx.Assert(DefaultValues.ContainsKey(varName), "Otherwise, we don't have a value to bind");

                boundParameters.Add(varName, _owner.UnescapeDefaultValue(DefaultValues[varName]));
            }

            public BindInformation PrepareBindInformation(IDictionary<string, string> parameters, bool omitDefaults)
            {
                if (parameters == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
                }

                string[] normalizedParameters = PrepareNormalizedParameters();
                IDictionary<string, string> extraParameters = null;
                foreach (string key in parameters.Keys)
                {
                    ProcessBindParameter(key, parameters[key], normalizedParameters, ref extraParameters);
                }

                ProcessDefaultsAndCreateBindInfo(omitDefaults, normalizedParameters, extraParameters, out BindInformation bindInfo);

                return bindInfo;
            }

            public BindInformation PrepareBindInformation(NameValueCollection parameters, bool omitDefaults)
            {
                if (parameters == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
                }

                string[] normalizedParameters = PrepareNormalizedParameters();
                IDictionary<string, string> extraParameters = null;
                foreach (string key in parameters.AllKeys)
                {
                    ProcessBindParameter(key, parameters[key], normalizedParameters, ref extraParameters);
                }

                ProcessDefaultsAndCreateBindInfo(omitDefaults, normalizedParameters, extraParameters, out BindInformation bindInfo);

                return bindInfo;
            }

            public BindInformation PrepareBindInformation(params string[] parameters)
            {
                if (parameters == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
                }

                if ((parameters.Length < _pathSegmentVariableNames.Count) ||
                    (parameters.Length > _pathSegmentVariableNames.Count + _queryValueVariableNames.Count))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                        SR.Format(SR.UTBindByPositionWrongCount, _owner._originalTemplate,
                        _pathSegmentVariableNames.Count, _queryValueVariableNames.Count,
                        parameters.Length)));
                }

                string[] normalizedParameters;
                if (parameters.Length == _pathSegmentVariableNames.Count + _queryValueVariableNames.Count)
                {
                    normalizedParameters = parameters;
                }
                else
                {
                    normalizedParameters = new string[_pathSegmentVariableNames.Count + _queryValueVariableNames.Count];
                    parameters.CopyTo(normalizedParameters, 0);
                    for (int i = parameters.Length; i < normalizedParameters.Length; i++)
                    {
                        normalizedParameters[i] = null;
                    }
                }

                LoadDefaultsAndValidate(normalizedParameters, out int lastNonDefaultPathParameter,
                    out int lastNonNullablePathParameter);

                return new BindInformation(normalizedParameters, lastNonDefaultPathParameter,
                    lastNonNullablePathParameter, _owner._additionalDefaults);
            }

            public void ValidateDefaults(out int firstOptionalSegment)
            {
                Fx.Assert(DefaultValues != null, "We are checking this condition from the c'tor");
                Fx.Assert(_pathSegmentVariableNames.Count > 0, "Otherwise, how can we have default values");

                // Finding the first valid nullable defaults
                for (int i = _pathSegmentVariableNames.Count - 1; (i >= 0) && (_firstNullablePathVariable == -1); i--)
                {
                    string varName = _pathSegmentVariableNames[i];
                    if (!DefaultValues.TryGetValue(varName, out string defaultValue))
                    {
                        _firstNullablePathVariable = i + 1;
                    }
                    else if (defaultValue != null)
                    {
                        _firstNullablePathVariable = i + 1;
                    }
                }

                if (_firstNullablePathVariable == -1)
                {
                    _firstNullablePathVariable = 0;
                }

                // Making sure that there are no nullables to the left of the first valid nullable
                if (_firstNullablePathVariable > 1)
                {
                    for (int i = _firstNullablePathVariable - 2; i >= 0; i--)
                    {
                        string varName = _pathSegmentVariableNames[i];
                        if (DefaultValues.TryGetValue(varName, out string defaultValue))
                        {
                            if (defaultValue == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                    SR.Format(SR.UTNullableDefaultMustBeFollowedWithNullables, _owner._originalTemplate,
                                    varName, _pathSegmentVariableNames[i + 1])));
                            }
                        }
                    }
                }

                // Making sure that there are no Literals\WildCards to the right
                // Based on the fact that only Variable Path Segments support default values,
                //  if firstNullablePathVariable=N and pathSegmentVariableNames.Count=M then
                //  the nature of the last M-N path segments should be StringNature.Variable; otherwise,
                //  there was a literal segment in between. Also, there shouldn't be a wildcard.
                if (_firstNullablePathVariable < _pathSegmentVariableNames.Count)
                {
                    if (_owner.HasWildcard)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                            SR.Format(SR.UTNullableDefaultMustNotBeFollowedWithWildcard,
                            _owner._originalTemplate, _pathSegmentVariableNames[_firstNullablePathVariable])));
                    }

                    for (int i = _pathSegmentVariableNames.Count - 1; i >= _firstNullablePathVariable; i--)
                    {
                        int segmentIndex = _owner._segments.Count - (_pathSegmentVariableNames.Count - i);
                        if (_owner._segments[segmentIndex].Nature != UriTemplatePartType.Variable)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.UTNullableDefaultMustNotBeFollowedWithLiteral,
                                _owner._originalTemplate, _pathSegmentVariableNames[_firstNullablePathVariable],
                                _owner._segments[segmentIndex].OriginalSegment)));
                        }
                    }
                }

                // Now that we have the firstNullablePathVariable set, lets calculate the firstOptionalSegment.
                //  We already knows that the last M-N path segments (when M=pathSegmentVariableNames.Count and
                //  N=firstNullablePathVariable) are optional (see the previos comment). We will start there and
                //  move to the left, stopping at the first segment, which is not a variable or is a variable
                //  and doesn't have a default value.
                int numNullablePathVariables = (_pathSegmentVariableNames.Count - _firstNullablePathVariable);
                firstOptionalSegment = _owner._segments.Count - numNullablePathVariables;
                if (!_owner.HasWildcard)
                {
                    while (firstOptionalSegment > 0)
                    {
                        UriTemplatePathSegment ps = _owner._segments[firstOptionalSegment - 1];
                        if (ps.Nature != UriTemplatePartType.Variable)
                        {
                            break;
                        }

                        UriTemplateVariablePathSegment vps = (ps as UriTemplateVariablePathSegment);
                        Fx.Assert(vps != null, "Should be; that's his nature");
                        if (!DefaultValues.ContainsKey(vps.VarName))
                        {
                            break;
                        }

                        firstOptionalSegment--;
                    }
                }
            }

            private void AddAdditionalDefaults(ref IDictionary<string, string> extraParameters)
            {
                if (extraParameters == null)
                {
                    extraParameters = _owner._additionalDefaults;
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in _owner._additionalDefaults)
                    {
                        if (!extraParameters.ContainsKey(kvp.Key))
                        {
                            extraParameters.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }

            private void LoadDefaultsAndValidate(string[] normalizedParameters, out int lastNonDefaultPathParameter,
                out int lastNonNullablePathParameter)
            {
                // First step - loading defaults
                for (int i = 0; i < _pathSegmentVariableNames.Count; i++)
                {
                    if (string.IsNullOrEmpty(normalizedParameters[i]) && (DefaultValues != null))
                    {
                        DefaultValues.TryGetValue(_pathSegmentVariableNames[i], out normalizedParameters[i]);
                    }
                }

                // Second step - calculating bind constrains
                lastNonDefaultPathParameter = _pathSegmentVariableNames.Count - 1;
                if ((DefaultValues != null) &&
                    (_owner._segments[_owner._segments.Count - 1].Nature != UriTemplatePartType.Literal))
                {
                    bool foundNonDefaultPathParameter = false;
                    while (!foundNonDefaultPathParameter && (lastNonDefaultPathParameter >= 0))
                    {
                        if (DefaultValues.TryGetValue(_pathSegmentVariableNames[lastNonDefaultPathParameter],
                            out string defaultValue))
                        {
                            if (string.Compare(normalizedParameters[lastNonDefaultPathParameter],
                                defaultValue, StringComparison.Ordinal) != 0)
                            {
                                foundNonDefaultPathParameter = true;
                            }
                            else
                            {
                                lastNonDefaultPathParameter--;
                            }
                        }
                        else
                        {
                            foundNonDefaultPathParameter = true;
                        }
                    }
                }

                if (_firstNullablePathVariable > lastNonDefaultPathParameter)
                {
                    lastNonNullablePathParameter = _firstNullablePathVariable - 1;
                }
                else
                {
                    lastNonNullablePathParameter = lastNonDefaultPathParameter;
                }

                // Third step - validate
                for (int i = 0; i <= lastNonNullablePathParameter; i++)
                {
                    // Skip validation for terminating star variable segment :
                    if (_owner.HasWildcard && _owner._wildcard.HasVariable &&
                        (i == _pathSegmentVariableNames.Count - 1))
                    {
                        continue;
                    }

                    // Validate
                    if (string.IsNullOrEmpty(normalizedParameters[i]))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(normalizedParameters),
                            SR.Format(SR.BindUriTemplateToNullOrEmptyPathParam, _pathSegmentVariableNames[i]));
                    }
                }
            }

            private void ParseVariableDeclaration(string varDeclaration, out string varName, out string defaultValue)
            {
                if ((varDeclaration.IndexOf('{') != -1) || (varDeclaration.IndexOf('}') != -1))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                        SR.Format(SR.UTInvalidVarDeclaration, _owner._originalTemplate, varDeclaration)));
                }

                int equalSignIndex = varDeclaration.IndexOf('=');
                switch (equalSignIndex)
                {
                    case -1:
                        varName = varDeclaration;
                        defaultValue = null;
                        break;

                    case 0:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                            SR.Format(SR.UTInvalidVarDeclaration, _owner._originalTemplate, varDeclaration)));

                    default:
                        varName = varDeclaration.Substring(0, equalSignIndex);
                        defaultValue = varDeclaration.Substring(equalSignIndex + 1);
                        if (defaultValue.IndexOf('=') != -1)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(
                                SR.Format(SR.UTInvalidVarDeclaration, _owner._originalTemplate, varDeclaration)));
                        }
                        break;
                }
            }

            private string[] PrepareNormalizedParameters()
            {
                string[] normalizedParameters = new string[_pathSegmentVariableNames.Count + _queryValueVariableNames.Count];
                for (int i = 0; i < normalizedParameters.Length; i++)
                {
                    normalizedParameters[i] = null;
                }

                return normalizedParameters;
            }

            private void ProcessBindParameter(string name, string value, string[] normalizedParameters,
                ref IDictionary<string, string> extraParameters)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(name),
                        SR.Format(SR.UTBindByNameCalledWithEmptyKey));
                }

                string uppercaseVarName = name.ToUpperInvariant();
                int pathVarIndex = _pathSegmentVariableNames.IndexOf(uppercaseVarName);
                if (pathVarIndex != -1)
                {
                    normalizedParameters[pathVarIndex] = (string.IsNullOrEmpty(value) ? string.Empty : value);
                    return;
                }

                int queryVarIndex = _queryValueVariableNames.IndexOf(uppercaseVarName);
                if (queryVarIndex != -1)
                {
                    normalizedParameters[_pathSegmentVariableNames.Count + queryVarIndex] = (string.IsNullOrEmpty(value) ? string.Empty : value);
                    return;
                }

                if (extraParameters == null)
                {
                    extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
                }

                extraParameters.Add(name, value);
            }

            private void ProcessDefaultsAndCreateBindInfo(bool omitDefaults, string[] normalizedParameters,
                IDictionary<string, string> extraParameters, out BindInformation bindInfo)
            {
                LoadDefaultsAndValidate(normalizedParameters, out int lastNonDefaultPathParameter,
                    out int lastNonNullablePathParameter);
                if (_owner._additionalDefaults != null)
                {
                    if (omitDefaults)
                    {
                        RemoveAdditionalDefaults(ref extraParameters);
                    }
                    else
                    {
                        AddAdditionalDefaults(ref extraParameters);
                    }
                }

                bindInfo = new BindInformation(normalizedParameters, lastNonDefaultPathParameter,
                    lastNonNullablePathParameter, extraParameters);
            }

            private void RemoveAdditionalDefaults(ref IDictionary<string, string> extraParameters)
            {
                if (extraParameters == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, string> kvp in _owner._additionalDefaults)
                {
                    if (extraParameters.TryGetValue(kvp.Key, out string extraParameter))
                    {
                        if (string.Compare(extraParameter, kvp.Value, StringComparison.Ordinal) == 0)
                        {
                            extraParameters.Remove(kvp.Key);
                        }
                    }
                }

                if (extraParameters.Count == 0)
                {
                    extraParameters = null;
                }
            }
        }

        internal class WildcardInfo
        {
            private readonly UriTemplate _owner;
            private readonly string _varName;

            public WildcardInfo(UriTemplate owner)
            {
                _varName = null;
                _owner = owner;
            }

            public WildcardInfo(UriTemplate owner, string segment)
            {
                Fx.Assert(!segment.EndsWith("/", StringComparison.Ordinal), "We are expecting to check this earlier");

                _varName = owner.AddPathVariable(UriTemplatePartType.Variable,
                    segment.Substring(1 + WildcardPath.Length, segment.Length - 2 - WildcardPath.Length),
                    out bool hasDefault);

                // Since this is a terminating star segment there shouldn't be a default
                if (hasDefault)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR. Format(SR.UTStarVariableWithDefaults, owner._originalTemplate,
                        segment, _varName)));
                }

                _owner = owner;
            }

            internal bool HasVariable
            {
                get
                {
                    return (!string.IsNullOrEmpty(_varName));
                }
            }

            public void Bind(string[] values, ref int valueIndex, StringBuilder path)
            {
                if (HasVariable)
                {
                    Fx.Assert(valueIndex < values.Length, "Not enough values to bind");
                    if (string.IsNullOrEmpty(values[valueIndex]))
                    {
                        valueIndex++;
                    }
                    else
                    {
                        path.Append(values[valueIndex++]);
                    }
                }
            }

            public void Lookup(int numMatchedSegments, Collection<string> relativePathSegments,
                NameValueCollection boundParameters)
            {
                Fx.Assert(numMatchedSegments == _owner._segments.Count, "We should have matched the other segments");

                if (HasVariable)
                {
                    StringBuilder remainingPath = new StringBuilder();
                    for (int i = numMatchedSegments; i < relativePathSegments.Count; i++)
                    {
                        if (i < relativePathSegments.Count - 1)
                        {
                            remainingPath.AppendFormat("{0}/", relativePathSegments[i]);
                        }
                        else
                        {
                            remainingPath.Append(relativePathSegments[i]);
                        }
                    }

                    boundParameters.Add(_varName, remainingPath.ToString());
                }
            }
        }
    }
}
