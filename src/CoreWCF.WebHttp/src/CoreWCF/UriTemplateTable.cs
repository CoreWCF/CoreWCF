// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public class UriTemplateTable
    {
        private Uri _baseAddress;
        private string _basePath;
        private Dictionary<string, FastPathInfo> _fastPathTable; // key is uri.PathAndQuery, fastPathTable may be null
        private bool _noTemplateHasQueryPart;
        private int _numSegmentsInBaseAddress;
        private UriTemplateTrieNode _rootNode;
        private readonly UriTemplatesCollection _templates;
        private readonly object _thisLock;
        private readonly bool _addTrailingSlashToBaseAddress;

        public UriTemplateTable()
            : this(null, null, true)
        {
        }

        public UriTemplateTable(IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs)
            : this(null, keyValuePairs, true)
        {
        }

        public UriTemplateTable(Uri baseAddress)
            : this(baseAddress, null, true)
        {
        }

        internal UriTemplateTable(Uri baseAddress, bool addTrailingSlashToBaseAddress)
            : this(baseAddress, null, addTrailingSlashToBaseAddress)
        {
        }

        public UriTemplateTable(Uri baseAddress, IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs)
            : this(baseAddress, keyValuePairs, true)
        {
        }

        internal UriTemplateTable(Uri baseAddress, IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs, bool addTrailingSlashToBaseAddress)
        {
            if (baseAddress != null && !baseAddress.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.UTTMustBeAbsolute);
            }

            _addTrailingSlashToBaseAddress = addTrailingSlashToBaseAddress;
            OriginalBaseAddress = baseAddress;

            if (keyValuePairs != null)
            {
                _templates = new UriTemplatesCollection(keyValuePairs);
            }
            else
            {
                _templates = new UriTemplatesCollection();
            }

            _thisLock = new object();
            _baseAddress = baseAddress;
            NormalizeBaseAddress();
        }

        public Uri BaseAddress
        {
            get
            {
                return _baseAddress;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                lock (_thisLock)
                {
                    if (IsReadOnly)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                            SR.UTTCannotChangeBaseAddress));
                    }
                    else
                    {
                        if (!value.IsAbsoluteUri)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.UTTBaseAddressMustBeAbsolute);
                        }
                        else
                        {
                            OriginalBaseAddress = value;
                            _baseAddress = value;
                            NormalizeBaseAddress();
                        }
                    }
                }
            }
        }

        public Uri OriginalBaseAddress { get; private set; }

        public bool IsReadOnly => _templates.IsFrozen;

        public IList<KeyValuePair<UriTemplate, object>> KeyValuePairs => _templates;

        public void MakeReadOnly(bool allowDuplicateEquivalentUriTemplates)
        {
            // idempotent
            lock (_thisLock)
            {
                if (!IsReadOnly)
                {
                    _templates.Freeze();
                    Validate(allowDuplicateEquivalentUriTemplates);
                    ConstructFastPathTable();
                }
            }
        }

        public Collection<UriTemplateMatch> Match(Uri uri)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(uri));
            }

            if (!uri.IsAbsoluteUri)
            {
                return None();
            }

            MakeReadOnly(true);

            // Matching path :
            if (!FastComputeRelativeSegmentsAndLookup(uri, out Collection<string> relativeSegments, out IList<UriTemplateTableMatchCandidate> candidates))
            {
                return None();
            }

            // Matching query :
            NameValueCollection queryParameters = null;
            if (!_noTemplateHasQueryPart && AtLeastOneCandidateHasQueryPart(candidates))
            {
                Collection<UriTemplateTableMatchCandidate> nextCandidates = new Collection<UriTemplateTableMatchCandidate>();
                Fx.Assert(nextCandidates.Count == 0, "nextCandidates should be empty");

                // then deal with query
                queryParameters = UriTemplateHelpers.ParseQueryString(uri.Query);
                bool mustBeEspeciallyInteresting = NoCandidateHasQueryLiteralRequirementsAndThereIsAnEmptyFallback(candidates);
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (UriTemplateHelpers.CanMatchQueryInterestingly(candidates[i].Template, queryParameters, mustBeEspeciallyInteresting))
                    {
                        nextCandidates.Add(candidates[i]);
                    }
                }

                if (nextCandidates.Count > 1)
                {
                    Fx.Assert(AllEquivalent(nextCandidates, 0, nextCandidates.Count), "demux algorithm problem, multiple non-equivalent matches");
                }

                if (nextCandidates.Count == 0)
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (UriTemplateHelpers.CanMatchQueryTrivially(candidates[i].Template))
                        {
                            nextCandidates.Add(candidates[i]);
                        }
                    }
                }

                if (nextCandidates.Count == 0)
                {
                    return None();
                }

                if (nextCandidates.Count > 1)
                {
                    Fx.Assert(AllEquivalent(nextCandidates, 0, nextCandidates.Count), "demux algorithm problem, multiple non-equivalent matches");
                }

                candidates = nextCandidates;
            }

            // Verifying that we have not broken the allowDuplicates settings because of terminal defaults
            //  This situation can be caused when we are hosting ".../" and ".../{foo=xyz}" in the same
            //  table. They are not equivalent; yet they reside together in the same path partially-equivalent
            //  set. If we hit a uri that ends up in that particular end-of-path set, we want to provide the
            //  user only the 'best' match and not both; thus preventing inconsistancy between the MakeReadonly
            //  settings and the matching results. We will assume that the 'best' matches will be the ones with
            //  the smallest number of segments - this will prefer ".../" over ".../{x=1}[/...]".
            if (NotAllCandidatesArePathFullyEquivalent(candidates))
            {
                Collection<UriTemplateTableMatchCandidate> nextCandidates = new Collection<UriTemplateTableMatchCandidate>();
                int minSegmentsCount = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    UriTemplateTableMatchCandidate candidate = candidates[i];
                    if (minSegmentsCount == -1)
                    {
                        minSegmentsCount = candidate.Template._segments.Count;
                        nextCandidates.Add(candidate);
                    }
                    else if (candidate.Template._segments.Count < minSegmentsCount)
                    {
                        minSegmentsCount = candidate.Template._segments.Count;
                        nextCandidates.Clear();
                        nextCandidates.Add(candidate);
                    }
                    else if (candidate.Template._segments.Count == minSegmentsCount)
                    {
                        nextCandidates.Add(candidate);
                    }
                }

                Fx.Assert(minSegmentsCount != -1, "At least the first entry in the list should be kept");
                Fx.Assert(nextCandidates.Count >= 1, "At least the first entry in the list should be kept");
                Fx.Assert(nextCandidates[0].Template._segments.Count == minSegmentsCount, "Trivial");

                candidates = nextCandidates;
            }

            // Building the actual result
            Collection<UriTemplateMatch> actualResults = new Collection<UriTemplateMatch>();
            for (int i = 0; i < candidates.Count; i++)
            {
                UriTemplateTableMatchCandidate candidate = candidates[i];
                UriTemplateMatch match = candidate.Template.CreateUriTemplateMatch(OriginalBaseAddress,
                    uri, candidate.Data, candidate.SegmentsCount, relativeSegments, queryParameters);
                actualResults.Add(match);
            }

            return actualResults;
        }

        public UriTemplateMatch MatchSingle(Uri uri)
        {
            Collection<UriTemplateMatch> c = Match(uri);
            if (c.Count == 0)
            {
                return null;
            }

            if (c.Count == 1)
            {
                return c[0];
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new UriTemplateMatchException(SR.UTTMultipleMatches));
        }

        private static bool AllEquivalent(IList<UriTemplateTableMatchCandidate> list, int a, int b)
        {
            for (int i = a; i < b - 1; ++i)
            {
                if (!list[i].Template.IsPathPartiallyEquivalentAt(list[i + 1].Template, list[i].SegmentsCount))
                {
                    return false;
                }

                if (!list[i].Template.IsQueryEquivalent(list[i + 1].Template))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AtLeastOneCandidateHasQueryPart(IList<UriTemplateTableMatchCandidate> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!UriTemplateHelpers.CanMatchQueryTrivially(candidates[i].Template))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NoCandidateHasQueryLiteralRequirementsAndThereIsAnEmptyFallback(
            IList<UriTemplateTableMatchCandidate> candidates)
        {
            bool thereIsAmEmptyFallback = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (UriTemplateHelpers.HasQueryLiteralRequirements(candidates[i].Template))
                {
                    return false;
                }

                if (candidates[i].Template._queries.Count == 0)
                {
                    thereIsAmEmptyFallback = true;
                }
            }

            return thereIsAmEmptyFallback;
        }

        private static Collection<UriTemplateMatch> None()
        {
            return new Collection<UriTemplateMatch>();
        }

        private static bool NotAllCandidatesArePathFullyEquivalent(IList<UriTemplateTableMatchCandidate> candidates)
        {
            if (candidates.Count <= 1)
            {
                return false;
            }

            int segmentsCount = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (segmentsCount == -1)
                {
                    segmentsCount = candidates[i].Template._segments.Count;
                }
                else if (segmentsCount != candidates[i].Template._segments.Count)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ComputeRelativeSegmentsAndLookup(Uri uri,
            ICollection<string> relativePathSegments, // add to this
            ICollection<UriTemplateTableMatchCandidate> candidates) // matched candidates
        {
            string[] uriSegments = uri.Segments;
            int numRelativeSegments = uriSegments.Length - _numSegmentsInBaseAddress;
            Fx.Assert(numRelativeSegments >= 0, "bad num segments");
            UriTemplateLiteralPathSegment[] uSegments = new UriTemplateLiteralPathSegment[numRelativeSegments];
            for (int i = 0; i < numRelativeSegments; ++i)
            {
                string seg = uriSegments[i + _numSegmentsInBaseAddress];
                // compute representation for matching
                UriTemplateLiteralPathSegment lps = UriTemplateLiteralPathSegment.CreateFromWireData(seg);
                uSegments[i] = lps;
                // compute representation to project out into results
                string relPathSeg = Uri.UnescapeDataString(seg);
                if (lps.EndsWithSlash)
                {
                    Fx.Assert(relPathSeg.EndsWith("/", StringComparison.Ordinal), "problem with relative path segment");
                    relPathSeg = relPathSeg.Substring(0, relPathSeg.Length - 1); // trim slash
                }

                relativePathSegments.Add(relPathSeg);
            }

            return _rootNode.Match(uSegments, candidates);
        }

        private void ConstructFastPathTable()
        {
            _noTemplateHasQueryPart = true;
            foreach (KeyValuePair<UriTemplate, object> kvp in _templates)
            {
                UriTemplate ut = kvp.Key;
                if (!UriTemplateHelpers.CanMatchQueryTrivially(ut))
                {
                    _noTemplateHasQueryPart = false;
                }

                if (ut.HasNoVariables && !ut.HasWildcard)
                {
                    // eligible for fast path
                    if (_fastPathTable == null)
                    {
                        _fastPathTable = new Dictionary<string, FastPathInfo>();
                    }

                    Uri uri = ut.BindByPosition(OriginalBaseAddress);
                    string uriPath = UriTemplateHelpers.GetUriPath(uri);
                    if (_fastPathTable.ContainsKey(uriPath))
                    {
                        // nothing to do, we've already seen it
                    }
                    else
                    {
                        FastPathInfo fpInfo = new FastPathInfo();
                        if (ComputeRelativeSegmentsAndLookup(uri, fpInfo.RelativePathSegments,
                            fpInfo.Candidates))
                        {
                            fpInfo.Freeze();
                            _fastPathTable.Add(uriPath, fpInfo);
                        }
                    }
                }
            }
        }

        // this method checks the literal cache for a match if none, goes through the slower path of cracking the segments
        private bool FastComputeRelativeSegmentsAndLookup(Uri uri, out Collection<string> relativePathSegments,
            out IList<UriTemplateTableMatchCandidate> candidates)
        {
            // Consider fast-path and lookup
            // return false if not under base uri
            string uriPath = UriTemplateHelpers.GetUriPath(uri);
            if ((_fastPathTable != null) && _fastPathTable.TryGetValue(uriPath, out FastPathInfo fpInfo))
            {
                relativePathSegments = fpInfo.RelativePathSegments;
                candidates = fpInfo.Candidates;
                VerifyThatFastPathAndSlowPathHaveSameResults(uri, relativePathSegments, candidates);
                return true;
            }
            else
            {
                relativePathSegments = new Collection<string>();
                candidates = new Collection<UriTemplateTableMatchCandidate>();
                return SlowComputeRelativeSegmentsAndLookup(uri, uriPath, relativePathSegments, candidates);
            }
        }

        private void NormalizeBaseAddress()
        {
            if (_baseAddress != null)
            {
                // ensure trailing slash on baseAddress, so that IsBaseOf will work later
                UriBuilder ub = new UriBuilder(_baseAddress);
                if (_addTrailingSlashToBaseAddress && !ub.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    ub.Path = ub.Path + "/";
                }

                ub.Host = "localhost"; // always normalize to localhost
                ub.Port = -1;
                ub.UserName = null;
                ub.Password = null;
                ub.Path = ub.Path.ToUpperInvariant();
                ub.Scheme = Uri.UriSchemeHttp;
                _baseAddress = ub.Uri;
                _basePath = UriTemplateHelpers.GetUriPath(_baseAddress);
            }
        }

        private bool SlowComputeRelativeSegmentsAndLookup(Uri uri, string uriPath, Collection<string> relativePathSegments,
            ICollection<UriTemplateTableMatchCandidate> candidates)
        {
            // ensure 'under' the base address
            if (uriPath.Length < _basePath.Length)
            {
                return false;
            }

            if (!uriPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else
            {
                // uriPath StartsWith basePath, but this check is not enough - basePath 'service1' should not match with uriPath 'service123'
                // make sure that after the match the next character is /, this is to avoid a uriPath of the form /service12/ matching with a basepath of the form /service1
                if (uriPath.Length > _basePath.Length && !_basePath.EndsWith("/", StringComparison.Ordinal) && uriPath[_basePath.Length] != '/')
                {
                    return false;
                }
            }

            return ComputeRelativeSegmentsAndLookup(uri, relativePathSegments, candidates);
        }

        private void Validate(bool allowDuplicateEquivalentUriTemplates)
        {
            if (_baseAddress == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.UTTBaseAddressNotSet));
            }

            _numSegmentsInBaseAddress = _baseAddress.Segments.Length;
            if (_templates.Count == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.UTTEmptyKeyValuePairs));
            }

            // build the trie and
            // validate that forall Uri u, at most one UriTemplate is a best match for u
            _rootNode = UriTemplateTrieNode.Make(_templates, allowDuplicateEquivalentUriTemplates);
        }

        [Conditional("DEBUG")]
        private void VerifyThatFastPathAndSlowPathHaveSameResults(Uri uri, Collection<string> fastPathRelativePathSegments,
            IList<UriTemplateTableMatchCandidate> fastPathCandidates)
        {
            Collection<string> slowPathRelativePathSegments = new Collection<string>();
            List<UriTemplateTableMatchCandidate> slowPathCandidates = new List<UriTemplateTableMatchCandidate>();
            if (!SlowComputeRelativeSegmentsAndLookup(uri, UriTemplateHelpers.GetUriPath(uri),
                slowPathRelativePathSegments, slowPathCandidates))
            {
                Fx.Assert("fast path yielded a result but slow path yielded no result");
            }

            // compare results
            if (fastPathRelativePathSegments.Count != slowPathRelativePathSegments.Count)
            {
                Fx.Assert("fast path yielded different number of segments from slow path");
            }

            for (int i = 0; i < fastPathRelativePathSegments.Count; ++i)
            {
                if (fastPathRelativePathSegments[i] != slowPathRelativePathSegments[i])
                {
                    Fx.Assert("fast path yielded different segments from slow path");
                }
            }

            if (fastPathCandidates.Count != slowPathCandidates.Count)
            {
                Fx.Assert("fast path yielded different number of candidates from slow path");
            }

            for (int i = 0; i < fastPathCandidates.Count; i++)
            {
                if (!slowPathCandidates.Contains(fastPathCandidates[i]))
                {
                    Fx.Assert("fast path yielded different candidates from slow path");
                }
            }
        }

        internal class FastPathInfo
        {
            private readonly FreezableCollection<UriTemplateTableMatchCandidate> _candidates;
            private readonly FreezableCollection<string> _relativePathSegments;

            public FastPathInfo()
            {
                _relativePathSegments = new FreezableCollection<string>();
                _candidates = new FreezableCollection<UriTemplateTableMatchCandidate>();
            }

            public Collection<UriTemplateTableMatchCandidate> Candidates => _candidates;

            public Collection<string> RelativePathSegments => _relativePathSegments;

            public void Freeze()
            {
                _relativePathSegments.Freeze();
                _candidates.Freeze();
            }
        }

        internal class UriTemplatesCollection : FreezableCollection<KeyValuePair<UriTemplate, object>>
        {
            public UriTemplatesCollection()
                : base()
            {
            }

            public UriTemplatesCollection(IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs)
                : base()
            {
                foreach (KeyValuePair<UriTemplate, object> kvp in keyValuePairs)
                {
                    ThrowIfInvalid(kvp.Key, "keyValuePairs");
                    Add(kvp);
                }
            }

            protected override void InsertItem(int index, KeyValuePair<UriTemplate, object> item)
            {
                ThrowIfInvalid(item.Key, "item");
                base.InsertItem(index, item);
            }

            protected override void SetItem(int index, KeyValuePair<UriTemplate, object> item)
            {
                ThrowIfInvalid(item.Key, "item");
                base.SetItem(index, item);
            }

            private static void ThrowIfInvalid(UriTemplate template, string argName)
            {
                if (template == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argName,
                        SR.UTTNullTemplateKey);
                }

                if (template.IgnoreTrailingSlash)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argName,
                        SR.Format(SR.UTTInvalidTemplateKey, template));
                }
            }
        }
    }
}
