// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal class UriTemplateTrieNode
    {
        private readonly int _depth; // relative segment depth (root = 0)
        private readonly UriTemplatePathPartiallyEquivalentSet _endOfPath; // matches the non-existent segment at the end of a slash-terminated path
        private AscendingSortedCompoundSegmentsCollection<UriTemplatePathPartiallyEquivalentSet> _finalCompoundSegment; // matches e.g. "{var}.{var}"
        private Dictionary<UriTemplateLiteralPathSegment, UriTemplatePathPartiallyEquivalentSet> _finalLiteralSegment; // matches e.g. "segmentThatDoesntEndInSlash"
        private readonly UriTemplatePathPartiallyEquivalentSet _finalVariableSegment; // matches e.g. "{var}"
        private AscendingSortedCompoundSegmentsCollection<UriTemplateTrieLocation> _nextCompoundSegment; // all are AfterLiteral; matches e.g. "{var}.{var}/"
        private Dictionary<UriTemplateLiteralPathSegment, UriTemplateTrieLocation> _nextLiteralSegment; // all are BeforeLiteral; matches e.g. "path/"
        private UriTemplateTrieLocation _nextVariableSegment; // is BeforeLiteral; matches e.g. "{var}/"
        private UriTemplateTrieLocation _onFailure; // points to parent, at 'after me'
        private readonly UriTemplatePathPartiallyEquivalentSet _star; // matches any "extra/path/segments" at the end

        private UriTemplateTrieNode(int depth)
        {
            _depth = depth;
            _nextLiteralSegment = null;
            _nextCompoundSegment = null;
            _finalLiteralSegment = null;
            _finalCompoundSegment = null;
            _finalVariableSegment = new UriTemplatePathPartiallyEquivalentSet(depth + 1);
            _star = new UriTemplatePathPartiallyEquivalentSet(depth);
            _endOfPath = new UriTemplatePathPartiallyEquivalentSet(depth);
        }

        public static UriTemplateTrieNode Make(IEnumerable<KeyValuePair<UriTemplate, object>> keyValuePairs,
            bool allowDuplicateEquivalentUriTemplates)
        {
            // given a UTT at MakeReadOnly time, build the trie
            // note that root.onFailure == null;
            UriTemplateTrieNode root = new UriTemplateTrieNode(0);
            foreach (KeyValuePair<UriTemplate, object> kvp in keyValuePairs)
            {
                Add(root, kvp);
            }

            Validate(root, allowDuplicateEquivalentUriTemplates);
            return root;
        }

        public bool Match(UriTemplateLiteralPathSegment[] wireData, ICollection<UriTemplateTableMatchCandidate> candidates)
        {
            UriTemplateTrieLocation currentLocation = new UriTemplateTrieLocation(this, UriTemplateTrieIntraNodeLocation.BeforeLiteral);
            return GetMatch(currentLocation, wireData, candidates);
        }

        private static void Add(UriTemplateTrieNode root, KeyValuePair<UriTemplate, object> kvp)
        {
            // Currently UTT doesn't support teplates with ignoreTrailingSlash == true; thus we
            //  don't care about supporting it in the trie as well.
            UriTemplateTrieNode current = root;
            UriTemplate ut = kvp.Key;
            bool needProcessingOnFinalNode = (ut._segments.Count == 0) || ut.HasWildcard ||
                ut._segments[ut._segments.Count - 1].EndsWithSlash;
            for (int i = 0; i < ut._segments.Count; ++i)
            {
                if (i >= ut._firstOptionalSegment)
                {
                    current._endOfPath.Items.Add(kvp);
                }

                UriTemplatePathSegment ps = ut._segments[i];
                if (!ps.EndsWithSlash)
                {
                    Fx.Assert(i == ut._segments.Count - 1, "only the last segment can !EndsWithSlash");
                    Fx.Assert(!ut.HasWildcard, "path star cannot have !EndsWithSlash");
                    switch (ps.Nature)
                    {
                        case UriTemplatePartType.Literal:
                            current.AddFinalLiteralSegment(ps as UriTemplateLiteralPathSegment, kvp);
                            break;

                        case UriTemplatePartType.Compound:
                            current.AddFinalCompoundSegment(ps as UriTemplateCompoundPathSegment, kvp);
                            break;

                        case UriTemplatePartType.Variable:
                            current._finalVariableSegment.Items.Add(kvp);
                            break;

                        default:
                            Fx.Assert("Invalid value as PathSegment.Nature");
                            break;
                    }
                }
                else
                {
                    Fx.Assert(ps.EndsWithSlash, "ps.EndsWithSlash");
                    switch (ps.Nature)
                    {
                        case UriTemplatePartType.Literal:
                            current = current.AddNextLiteralSegment(ps as UriTemplateLiteralPathSegment);
                            break;

                        case UriTemplatePartType.Compound:
                            current = current.AddNextCompoundSegment(ps as UriTemplateCompoundPathSegment);
                            break;

                        case UriTemplatePartType.Variable:
                            current = current.AddNextVariableSegment();
                            break;

                        default:
                            Fx.Assert("Invalid value as PathSegment.Nature");
                            break;
                    }
                }
            }

            if (needProcessingOnFinalNode)
            {
                // if the last segment ended in a slash, there is still more to do
                if (ut.HasWildcard)
                {
                    // e.g. "path1/path2/*"
                    current._star.Items.Add(kvp);
                }
                else
                {
                    // e.g. "path1/path2/"
                    current._endOfPath.Items.Add(kvp);
                }
            }
        }

        private static bool CheckMultipleMatches(IList<IList<UriTemplateTrieLocation>> locationsSet, UriTemplateLiteralPathSegment[] wireData,
            ICollection<UriTemplateTableMatchCandidate> candidates)
        {
            bool result = false;
            for (int i = 0; ((i < locationsSet.Count) && !result); i++)
            {
                for (int j = 0; j < locationsSet[i].Count; j++)
                {
                    if (GetMatch(locationsSet[i][j], wireData, candidates))
                    {
                        result = true;
                    }
                }
            }

            return result;
        }

        private static bool GetMatch(UriTemplateTrieLocation location, UriTemplateLiteralPathSegment[] wireData,
            ICollection<UriTemplateTableMatchCandidate> candidates)
        {
            int initialDepth = location.Node._depth;
            do
            {
                if (TryMatch(wireData, location, out UriTemplatePathPartiallyEquivalentSet answer, out SingleLocationOrLocationsSet nextStep))
                {
                    if (answer != null)
                    {
                        for (int i = 0; i < answer.Items.Count; i++)
                        {
                            candidates.Add(new UriTemplateTableMatchCandidate(answer.Items[i].Key, answer.SegmentsCount,
                                answer.Items[i].Value));
                        }
                    }

                    return true;
                }

                if (nextStep.IsSingle)
                {
                    location = nextStep.SingleLocation;
                }
                else
                {
                    Fx.Assert(nextStep.LocationsSet != null, "This should be set to a valid value by TryMatch");
                    if (CheckMultipleMatches(nextStep.LocationsSet, wireData, candidates))
                    {
                        return true;
                    }
                    location = GetFailureLocationFromLocationsSet(nextStep.LocationsSet);
                }
            } while ((location != null) && (location.Node._depth >= initialDepth));

            // we walked the whole trie down and found nothing
            return false;
        }

        private static bool TryMatch(UriTemplateLiteralPathSegment[] wireUriSegments, UriTemplateTrieLocation currentLocation,
            out UriTemplatePathPartiallyEquivalentSet success, out SingleLocationOrLocationsSet nextStep)
        {
            // if returns true, success is set to answer
            // if returns false, nextStep is set to next place to look
            success = null;
            nextStep = new SingleLocationOrLocationsSet();

            if (wireUriSegments.Length <= currentLocation.Node._depth)
            {
                Fx.Assert(wireUriSegments.Length == 0 || wireUriSegments[wireUriSegments.Length - 1].EndsWithSlash,
                    "we should not have traversed this deep into the trie unless the wire path ended in a slash");

                if (currentLocation.Node._endOfPath.Items.Count != 0)
                {
                    // exact match of e.g. "path1/path2/"
                    success = currentLocation.Node._endOfPath;
                    return true;
                }
                else if (currentLocation.Node._star.Items.Count != 0)
                {
                    // inexact match of e.g. WIRE("path1/path2/") against TEMPLATE("path1/path2/*")
                    success = currentLocation.Node._star;
                    return true;
                }
                else
                {
                    nextStep = new SingleLocationOrLocationsSet(currentLocation.Node._onFailure);
                    return false;
                }
            }
            else
            {
                UriTemplateLiteralPathSegment curWireSeg = wireUriSegments[currentLocation.Node._depth];
                bool considerLiteral = false;
                bool considerCompound = false;
                bool considerVariable = false;
                bool considerStar = false;
                switch (currentLocation.LocationWithin)
                {
                    case UriTemplateTrieIntraNodeLocation.BeforeLiteral:
                        considerLiteral = true;
                        considerCompound = true;
                        considerVariable = true;
                        considerStar = true;
                        break;
                    case UriTemplateTrieIntraNodeLocation.AfterLiteral:
                        considerLiteral = false;
                        considerCompound = true;
                        considerVariable = true;
                        considerStar = true;
                        break;
                    case UriTemplateTrieIntraNodeLocation.AfterCompound:
                        considerLiteral = false;
                        considerCompound = false;
                        considerVariable = true;
                        considerStar = true;
                        break;
                    case UriTemplateTrieIntraNodeLocation.AfterVariable:
                        considerLiteral = false;
                        considerCompound = false;
                        considerVariable = false;
                        considerStar = true;
                        break;
                    default:
                        Fx.Assert("bad kind");
                        break;
                }

                if (curWireSeg.EndsWithSlash)
                {

                    if (considerLiteral && currentLocation.Node._nextLiteralSegment != null &&
                        currentLocation.Node._nextLiteralSegment.ContainsKey(curWireSeg))
                    {
                        nextStep = new SingleLocationOrLocationsSet(currentLocation.Node._nextLiteralSegment[curWireSeg]);
                        return false;
                    }
                    else if (considerCompound && currentLocation.Node._nextCompoundSegment != null &&
                        AscendingSortedCompoundSegmentsCollection<UriTemplateTrieLocation>.Lookup(currentLocation.Node._nextCompoundSegment, curWireSeg, out IList<IList<UriTemplateTrieLocation>> compoundLocationsSet))
                    {
                        nextStep = new SingleLocationOrLocationsSet(compoundLocationsSet);
                        return false;
                    }
                    else if (considerVariable && currentLocation.Node._nextVariableSegment != null &&
                        !curWireSeg.IsNullOrEmpty())
                    {
                        nextStep = new SingleLocationOrLocationsSet(currentLocation.Node._nextVariableSegment);
                        return false;
                    }
                    else if (considerStar && currentLocation.Node._star.Items.Count != 0)
                    {
                        // matches e.g. WIRE("path1/path2/path3") and TEMPLATE("path1/*")
                        success = currentLocation.Node._star;
                        return true;
                    }
                    else
                    {
                        nextStep = new SingleLocationOrLocationsSet(currentLocation.Node._onFailure);
                        return false;
                    }
                }
                else
                {
                    Fx.Assert(!curWireSeg.EndsWithSlash, "!curWireSeg.EndsWithSlash");
                    Fx.Assert(!curWireSeg.IsNullOrEmpty(), "!curWireSeg.IsNullOrEmpty()");

                    if (considerLiteral && currentLocation.Node._finalLiteralSegment != null &&
                        currentLocation.Node._finalLiteralSegment.ContainsKey(curWireSeg))
                    {
                        // matches e.g. WIRE("path1/path2") and TEMPLATE("path1/path2")
                        success = currentLocation.Node._finalLiteralSegment[curWireSeg];
                        return true;
                    }
                    else if (considerCompound && currentLocation.Node._finalCompoundSegment != null &&
                        AscendingSortedCompoundSegmentsCollection<UriTemplatePathPartiallyEquivalentSet>.Lookup(currentLocation.Node._finalCompoundSegment, curWireSeg, out IList<IList<UriTemplatePathPartiallyEquivalentSet>> compoundPathEquivalentSets))
                    {
                        // matches e.g. WIRE("path1/path2") and TEMPLATE("path1/p{var}th2")
                        // we should take only the highest order match!
                        Fx.Assert(compoundPathEquivalentSets.Count >= 1, "Lookup is expected to return false otherwise");
                        Fx.Assert(compoundPathEquivalentSets[0].Count > 0, "Find shouldn't return empty sublists");

                        if (compoundPathEquivalentSets[0].Count == 1)
                        {
                            success = compoundPathEquivalentSets[0][0];
                        }
                        else
                        {
                            success = new UriTemplatePathPartiallyEquivalentSet(currentLocation.Node._depth + 1);
                            for (int i = 0; i < compoundPathEquivalentSets[0].Count; i++)
                            {
                                success.Items.AddRange(compoundPathEquivalentSets[0][i].Items);
                            }
                        }

                        return true;
                    }
                    else if (considerVariable && currentLocation.Node._finalVariableSegment.Items.Count != 0)
                    {
                        // matches e.g. WIRE("path1/path2") and TEMPLATE("path1/{var}")
                        success = currentLocation.Node._finalVariableSegment;

                        return true;
                    }
                    else if (considerStar && currentLocation.Node._star.Items.Count != 0)
                    {
                        // matches e.g. WIRE("path1/path2") and TEMPLATE("path1/*")
                        success = currentLocation.Node._star;

                        return true;
                    }
                    else
                    {
                        nextStep = new SingleLocationOrLocationsSet(currentLocation.Node._onFailure);

                        return false;
                    }
                }
            }
        }

        private static UriTemplateTrieLocation GetFailureLocationFromLocationsSet(IList<IList<UriTemplateTrieLocation>> locationsSet)
        {
            Fx.Assert(locationsSet != null, "Shouldn't be called on null set");
            Fx.Assert(locationsSet.Count > 0, "Shouldn't be called on empty set");
            Fx.Assert(locationsSet[0] != null, "Shouldn't be called on a set with null sub-lists");
            Fx.Assert(locationsSet[0].Count > 0, "Shouldn't be called on a set with empty sub-lists");

            return locationsSet[0][0].Node._onFailure;
        }

        private static void Validate(UriTemplateTrieNode root, bool allowDuplicateEquivalentUriTemplates)
        {
            // walk the entire tree, and ensure that each PathEquivalentSet is ok (no ambiguous queries),
            // verify the compound segments didn't add potentially multiple matches;
            // also Assert various data-structure invariants
            Queue<UriTemplateTrieNode> nodesQueue = new Queue<UriTemplateTrieNode>();

            UriTemplateTrieNode current = root;
            while (true)
            {
                // validate all the PathEquivalentSets that live in this node
                Validate(current._endOfPath, allowDuplicateEquivalentUriTemplates);
                Validate(current._finalVariableSegment, allowDuplicateEquivalentUriTemplates);
                Validate(current._star, allowDuplicateEquivalentUriTemplates);
                if (current._finalLiteralSegment != null)
                {
                    foreach (KeyValuePair<UriTemplateLiteralPathSegment, UriTemplatePathPartiallyEquivalentSet> kvp in current._finalLiteralSegment)
                    {
                        Validate(kvp.Value, allowDuplicateEquivalentUriTemplates);
                    }
                }

                if (current._finalCompoundSegment != null)
                {
                    IList<IList<UriTemplatePathPartiallyEquivalentSet>> pesLists = current._finalCompoundSegment.Values;
                    for (int i = 0; i < pesLists.Count; i++)
                    {
                        if (!allowDuplicateEquivalentUriTemplates && (pesLists[i].Count > 1))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                                SR.UTTDuplicate, pesLists[i][0].Items[0].Key.ToString(), pesLists[i][1].Items[0].Key.ToString())));
                        }
                        for (int j = 0; j < pesLists[i].Count; j++)
                        {
                            Validate(pesLists[i][j], allowDuplicateEquivalentUriTemplates);
                        }
                    }
                }

                // deal with children of this node
                if (current._nextLiteralSegment != null)
                {
                    foreach (KeyValuePair<UriTemplateLiteralPathSegment, UriTemplateTrieLocation> kvp in current._nextLiteralSegment)
                    {
                        Fx.Assert(kvp.Value.LocationWithin == UriTemplateTrieIntraNodeLocation.BeforeLiteral, "forward-pointers should always point to a BeforeLiteral location");
                        Fx.Assert(kvp.Value.Node._depth == current._depth + 1, "kvp.Value.node.depth == current.depth + 1");
                        Fx.Assert(kvp.Value.Node._onFailure.Node == current, "back pointer should point back to here");
                        Fx.Assert(kvp.Value.Node._onFailure.LocationWithin == UriTemplateTrieIntraNodeLocation.AfterLiteral, "back-pointer should be AfterLiteral");
                        nodesQueue.Enqueue(kvp.Value.Node);
                    }
                }

                if (current._nextCompoundSegment != null)
                {
                    IList<IList<UriTemplateTrieLocation>> locations = current._nextCompoundSegment.Values;
                    for (int i = 0; i < locations.Count; i++)
                    {
                        if (!allowDuplicateEquivalentUriTemplates && (locations[i].Count > 1))
                        {
                            // In the future we might ease up the restrictions and verify if there is realy
                            // a potential multiple match here; for now we are throwing.
                            UriTemplate firstTemplate = FindAnyUriTemplate(locations[i][0].Node);
                            UriTemplate secondTemplate = FindAnyUriTemplate(locations[i][1].Node);
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                                SR.UTTDuplicate, firstTemplate.ToString(), secondTemplate.ToString())));
                        }

                        for (int j = 0; j < locations[i].Count; j++)
                        {
                            UriTemplateTrieLocation location = locations[i][j];

                            Fx.Assert(location.LocationWithin == UriTemplateTrieIntraNodeLocation.BeforeLiteral, "forward-pointers should always point to a BeforeLiteral location");
                            Fx.Assert(location.Node._depth == current._depth + 1, "kvp.Value.node.depth == current.depth + 1");
                            Fx.Assert(location.Node._onFailure.Node == current, "back pointer should point back to here");
                            Fx.Assert(location.Node._onFailure.LocationWithin == UriTemplateTrieIntraNodeLocation.AfterCompound, "back-pointer should be AfterCompound");

                            nodesQueue.Enqueue(location.Node);
                        }
                    }
                }

                if (current._nextVariableSegment != null)
                {
                    Fx.Assert(current._nextVariableSegment.LocationWithin == UriTemplateTrieIntraNodeLocation.BeforeLiteral, "forward-pointers should always point to a BeforeLiteral location");
                    Fx.Assert(current._nextVariableSegment.Node._depth == current._depth + 1, "current.nextVariableSegment.node.depth == current.depth + 1");
                    Fx.Assert(current._nextVariableSegment.Node._onFailure.Node == current, "back pointer should point back to here");
                    Fx.Assert(current._nextVariableSegment.Node._onFailure.LocationWithin == UriTemplateTrieIntraNodeLocation.AfterVariable, "back-pointer should be AfterVariable");

                    nodesQueue.Enqueue(current._nextVariableSegment.Node);
                }

                // move on to next bit of work
                if (nodesQueue.Count == 0)
                {
                    break;
                }

                current = nodesQueue.Dequeue();
            }
        }

        private static void Validate(UriTemplatePathPartiallyEquivalentSet pes, bool allowDuplicateEquivalentUriTemplates)
        {
            // A set with 0 or 1 items is valid by definition
            if (pes.Items.Count < 2)
            {
                return;
            }

            // Assert all paths are partially-equivalent
            for (int i = 0; i < pes.Items.Count - 1; ++i)
            {
                Fx.Assert(pes.Items[i].Key.IsPathPartiallyEquivalentAt(pes.Items[i + 1].Key, pes.SegmentsCount),
                    "all elements of a PES must be path partially-equivalent");
            }

            // We will check that the queries disambiguate only for templates, which are
            //  matched completely at the segments count; templates, which are match at
            //  that point due to terminal defaults, will be ruled out.
            UriTemplate[] a = new UriTemplate[pes.Items.Count];
            int arrayIndex = 0;
            foreach (KeyValuePair<UriTemplate, object> kvp in pes.Items)
            {
                if (pes.SegmentsCount < kvp.Key._segments.Count)
                {
                    continue;
                }

                Fx.Assert(arrayIndex < a.Length, "We made enough room for all the items");

                a[arrayIndex++] = kvp.Key;
            }

            // Ensure that queries disambiguate (if needed) :
            if (arrayIndex > 0)
            {
                UriTemplateHelpers.DisambiguateSamePath(a, 0, arrayIndex, allowDuplicateEquivalentUriTemplates);
            }
        }

        private static UriTemplate FindAnyUriTemplate(UriTemplateTrieNode node)
        {
            while (node != null)
            {
                if (node._endOfPath.Items.Count > 0)
                {
                    return node._endOfPath.Items[0].Key;
                }

                if (node._finalVariableSegment.Items.Count > 0)
                {
                    return node._finalVariableSegment.Items[0].Key;
                }

                if (node._star.Items.Count > 0)
                {
                    return node._star.Items[0].Key;
                }

                if (node._finalLiteralSegment != null)
                {
                    UriTemplatePathPartiallyEquivalentSet pes =
                        GetAnyDictionaryValue(node._finalLiteralSegment);

                    Fx.Assert(pes.Items.Count > 0, "Otherwise, why creating the dictionary?");

                    return pes.Items[0].Key;
                }

                if (node._finalCompoundSegment != null)
                {
                    UriTemplatePathPartiallyEquivalentSet pes = node._finalCompoundSegment.GetAnyValue();

                    Fx.Assert(pes.Items.Count > 0, "Otherwise, why creating the collection?");

                    return pes.Items[0].Key;
                }

                if (node._nextLiteralSegment != null)
                {
                    UriTemplateTrieLocation location =
                        GetAnyDictionaryValue(node._nextLiteralSegment);
                    node = location.Node;
                }
                else if (node._nextCompoundSegment != null)
                {
                    UriTemplateTrieLocation location = node._nextCompoundSegment.GetAnyValue();
                    node = location.Node;
                }
                else if (node._nextVariableSegment != null)
                {
                    node = node._nextVariableSegment.Node;
                }
                else
                {
                    node = null;
                }
            }

            Fx.Assert("How did we got here without finding a UriTemplate earlier?");

            return null;
        }

        private static T GetAnyDictionaryValue<T>(IDictionary<UriTemplateLiteralPathSegment, T> dictionary)
        {
            using (IEnumerator<T> valuesEnumerator = dictionary.Values.GetEnumerator())
            {
                valuesEnumerator.MoveNext();
                return valuesEnumerator.Current;
            }
        }

        private void AddFinalCompoundSegment(UriTemplateCompoundPathSegment cps, KeyValuePair<UriTemplate, object> kvp)
        {
            Fx.Assert(cps != null, "must be - based on the segment nature");

            if (_finalCompoundSegment == null)
            {
                _finalCompoundSegment = new AscendingSortedCompoundSegmentsCollection<UriTemplatePathPartiallyEquivalentSet>();
            }

            UriTemplatePathPartiallyEquivalentSet pes = _finalCompoundSegment.Find(cps);
            if (pes == null)
            {
                pes = new UriTemplatePathPartiallyEquivalentSet(_depth + 1);
                _finalCompoundSegment.Add(cps, pes);
            }

            pes.Items.Add(kvp);
        }

        private void AddFinalLiteralSegment(UriTemplateLiteralPathSegment lps, KeyValuePair<UriTemplate, object> kvp)
        {
            Fx.Assert(lps != null, "must be - based on the segment nature");

            if (_finalLiteralSegment != null && _finalLiteralSegment.ContainsKey(lps))
            {
                _finalLiteralSegment[lps].Items.Add(kvp);
            }
            else
            {
                if (_finalLiteralSegment == null)
                {
                    _finalLiteralSegment = new Dictionary<UriTemplateLiteralPathSegment, UriTemplatePathPartiallyEquivalentSet>();
                }

                UriTemplatePathPartiallyEquivalentSet pes = new UriTemplatePathPartiallyEquivalentSet(_depth + 1);
                pes.Items.Add(kvp);
                _finalLiteralSegment.Add(lps, pes);
            }
        }

        private UriTemplateTrieNode AddNextCompoundSegment(UriTemplateCompoundPathSegment cps)
        {
            Fx.Assert(cps != null, "must be - based on the segment nature");

            if (_nextCompoundSegment == null)
            {
                _nextCompoundSegment = new AscendingSortedCompoundSegmentsCollection<UriTemplateTrieLocation>();
            }

            UriTemplateTrieLocation nextLocation = _nextCompoundSegment.Find(cps);
            if (nextLocation == null)
            {
                UriTemplateTrieNode nextNode = new UriTemplateTrieNode(_depth + 1);
                nextNode._onFailure = new UriTemplateTrieLocation(this, UriTemplateTrieIntraNodeLocation.AfterCompound);
                nextLocation = new UriTemplateTrieLocation(nextNode, UriTemplateTrieIntraNodeLocation.BeforeLiteral);
                _nextCompoundSegment.Add(cps, nextLocation);
            }

            return nextLocation.Node;
        }

        private UriTemplateTrieNode AddNextLiteralSegment(UriTemplateLiteralPathSegment lps)
        {
            Fx.Assert(lps != null, "must be - based on the segment nature");

            if (_nextLiteralSegment != null && _nextLiteralSegment.ContainsKey(lps))
            {
                return _nextLiteralSegment[lps].Node;
            }
            else
            {
                if (_nextLiteralSegment == null)
                {
                    _nextLiteralSegment = new Dictionary<UriTemplateLiteralPathSegment, UriTemplateTrieLocation>();
                }

                UriTemplateTrieNode newNode = new UriTemplateTrieNode(_depth + 1);
                newNode._onFailure = new UriTemplateTrieLocation(this, UriTemplateTrieIntraNodeLocation.AfterLiteral);
                _nextLiteralSegment.Add(lps, new UriTemplateTrieLocation(newNode, UriTemplateTrieIntraNodeLocation.BeforeLiteral));

                return newNode;
            }
        }

        private UriTemplateTrieNode AddNextVariableSegment()
        {
            if (_nextVariableSegment != null)
            {
                return _nextVariableSegment.Node;
            }
            else
            {
                UriTemplateTrieNode newNode = new UriTemplateTrieNode(_depth + 1);
                newNode._onFailure = new UriTemplateTrieLocation(this, UriTemplateTrieIntraNodeLocation.AfterVariable);
                _nextVariableSegment = new UriTemplateTrieLocation(newNode, UriTemplateTrieIntraNodeLocation.BeforeLiteral);

                return newNode;
            }
        }

        internal struct SingleLocationOrLocationsSet
        {
            private readonly IList<IList<UriTemplateTrieLocation>> _locationsSet;
            private readonly UriTemplateTrieLocation _singleLocation;

            public SingleLocationOrLocationsSet(UriTemplateTrieLocation singleLocation)
            {
                IsSingle = true;
                _singleLocation = singleLocation;
                _locationsSet = null;
            }

            public SingleLocationOrLocationsSet(IList<IList<UriTemplateTrieLocation>> locationsSet)
            {
                IsSingle = false;
                _singleLocation = null;
                _locationsSet = locationsSet;
            }

            public bool IsSingle { get; }

            public IList<IList<UriTemplateTrieLocation>> LocationsSet
            {
                get
                {
                    Fx.Assert(!IsSingle, "!this.isSingle");

                    return _locationsSet;
                }
            }

            public UriTemplateTrieLocation SingleLocation
            {
                get
                {
                    Fx.Assert(IsSingle, "this.isSingle");

                    return _singleLocation;
                }
            }
        }

        internal class AscendingSortedCompoundSegmentsCollection<T>
            where T : class
        {
            private readonly SortedList<UriTemplateCompoundPathSegment, Collection<CollectionItem>> _items;

            public AscendingSortedCompoundSegmentsCollection()
            {
                _items = new SortedList<UriTemplateCompoundPathSegment, Collection<AscendingSortedCompoundSegmentsCollection<T>.CollectionItem>>();
            }

            public IList<IList<T>> Values
            {
                get
                {
                    IList<IList<T>> results = new List<IList<T>>(_items.Count);
                    for (int i = 0; i < _items.Values.Count; i++)
                    {
                        results.Add(new List<T>(_items.Values[i].Count));
                        Fx.Assert(results.Count == i + 1, "We are adding item for each values collection");
                        for (int j = 0; j < _items.Values[i].Count; j++)
                        {
                            results[i].Add(_items.Values[i][j].Value);
                            Fx.Assert(results[i].Count == j + 1, "We are adding item for each value in the collection");
                        }

                        Fx.Assert(results[i].Count == _items.Values[i].Count, "We were supposed to add an item for each value in the collection");
                    }

                    Fx.Assert(results.Count == _items.Values.Count, "We were supposed to add a sub-list for each values collection");

                    return results;
                }
            }

            public void Add(UriTemplateCompoundPathSegment segment, T value)
            {
                int index = _items.IndexOfKey(segment);
                if (index == -1)
                {
                    Collection<CollectionItem> subItems = new Collection<CollectionItem>
                    {
                        new CollectionItem(segment, value)
                    };
                    _items.Add(segment, subItems);
                }
                else
                {
                    Collection<CollectionItem> subItems = _items.Values[index];
                    subItems.Add(new CollectionItem(segment, value));
                }
            }

            public T Find(UriTemplateCompoundPathSegment segment)
            {
                int index = _items.IndexOfKey(segment);
                if (index == -1)
                {
                    return null;
                }

                Collection<CollectionItem> subItems = this._items.Values[index];
                for (int i = 0; i < subItems.Count; i++)
                {
                    if (subItems[i].Segment.IsEquivalentTo(segment, false))
                    {
                        return subItems[i].Value;
                    }
                }

                return null;
            }

            public IList<IList<T>> Find(UriTemplateLiteralPathSegment wireData)
            {
                IList<IList<T>> results = new List<IList<T>>();
                for (int i = 0; i < _items.Values.Count; i++)
                {
                    List<T> sameOrderResults = null;
                    for (int j = 0; j < _items.Values[i].Count; j++)
                    {
                        if (_items.Values[i][j].Segment.IsMatch(wireData))
                        {
                            if (sameOrderResults == null)
                            {
                                sameOrderResults = new List<T>();
                            }
                            sameOrderResults.Add(_items.Values[i][j].Value);
                        }
                    }

                    if (sameOrderResults != null)
                    {
                        results.Add(sameOrderResults);
                    }
                }

                return results;
            }

            public T GetAnyValue()
            {
                if (_items.Values.Count > 0)
                {
                    Fx.Assert(_items.Values[0].Count > 0, "We are not adding a sub-list unless there is at list one item");

                    return _items.Values[0][0].Value;
                }
                else
                {
                    return null;
                }
            }

            public static bool Lookup(AscendingSortedCompoundSegmentsCollection<T> collection,
                UriTemplateLiteralPathSegment wireData, out IList<IList<T>> results)
            {
                results = collection.Find(wireData);
                return (results != null) && (results.Count > 0);
            }

            internal struct CollectionItem
            {
                public CollectionItem(UriTemplateCompoundPathSegment segment, T value)
                {
                    Segment = segment;
                    Value = value;
                }

                public UriTemplateCompoundPathSegment Segment { get; }

                public T Value { get; }
            }
        }
    }
}
