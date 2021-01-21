// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Collections;

namespace CoreWCF.Channels
{
    internal sealed class UriPrefixTable<TItem>
        where TItem : class
    {
        int count;
        const int HopperSize = 128;
        volatile HopperCache lookupCache; // cache matches, for lookup speed
        AsyncLock _asyncLock = new AsyncLock();

        SegmentHierarchyNode<TItem> root;
        bool useWeakReferences;
        bool includePortInComparison;

        public UriPrefixTable()
            : this(false)
        {
        }

        public UriPrefixTable(bool includePortInComparison)
            : this(includePortInComparison, false)
        {
        }

        public UriPrefixTable(bool includePortInComparison, bool useWeakReferences)
        {
            this.includePortInComparison = includePortInComparison;
            this.useWeakReferences = useWeakReferences;
            root = new SegmentHierarchyNode<TItem>(null, useWeakReferences);
            lookupCache = new HopperCache(HopperSize, useWeakReferences);
        }

        internal UriPrefixTable(UriPrefixTable<TItem> objectToClone)
            : this(objectToClone.includePortInComparison, objectToClone.useWeakReferences)
        {
            if (objectToClone.Count > 0)
            {
                foreach (KeyValuePair<BaseUriWithWildcard, TItem> current in objectToClone.GetAll())
                {
                    RegisterUri(current.Key.BaseAddress, current.Key.HostNameComparisonMode, current.Value);
                }
            }
        }

        object ThisLock
        {
            get
            {
                // The UriPrefixTable instance itself is used as a 
                // synchronization primitive in the TransportManagers and the 
                // TransportManagerContainers so we return 'this' to keep them in sync.                 
                return this;
            }
        }

        public int Count
        {
            get
            {
                return count;
            }
        }

        public AsyncLock AsyncLock { get { return _asyncLock; } }

        public bool IsRegistered(BaseUriWithWildcard key)
        {
            Uri uri = key.BaseAddress;

            // don't need to normalize path since SegmentHierarchyNode is 
            // already OrdinalIgnoreCase
            string[] paths = UriSegmenter.ToPath(uri, key.HostNameComparisonMode, includePortInComparison);
            bool exactMatch;
            SegmentHierarchyNode<TItem> node;
            using (AsyncLock.TakeLock())
            {
                node = FindDataNode(paths, out exactMatch);
            }
            return exactMatch && node != null && node.Data != null;
        }

        public IEnumerable<KeyValuePair<BaseUriWithWildcard, TItem>> GetAll()
        {
            using (AsyncLock.TakeLock())
            {
                List<KeyValuePair<BaseUriWithWildcard, TItem>> result = new List<KeyValuePair<BaseUriWithWildcard, TItem>>();
                root.Collect(result);
                return result;
            }
        }

        bool TryCacheLookup(BaseUriWithWildcard key, out TItem item)
        {
            object value = lookupCache.GetValue(ThisLock, key);

            // We might return null and true in the case of DBNull (cached negative result).
            // When TItem is object, the cast isn't sufficient to weed out DBNulls, so we need an explicit check.
            item = value == DBNull.Value ? null : (TItem)value;
            return value != null;
        }

        void AddToCache(BaseUriWithWildcard key, TItem item)
        {
            // Don't allow explicitly adding DBNulls.
            Fx.Assert(item != DBNull.Value, "Can't add DBNull to UriPrefixTable.");

            // HopperCache uses null as 'doesn't exist', so use DBNull as a stand-in for null.
            lookupCache.Add(key, item ?? (object)DBNull.Value);
        }

        void ClearCache()
        {
            lookupCache = new HopperCache(HopperSize, useWeakReferences);
        }

        public bool TryLookupUri(Uri uri, HostNameComparisonMode hostNameComparisonMode, out TItem item)
        {
            BaseUriWithWildcard key = new BaseUriWithWildcard(uri, hostNameComparisonMode);
            if (TryCacheLookup(key, out item))
            {
                return item != null;
            }

            using (AsyncLock.TakeLock())
            {
                // exact match failed, perform the full lookup (which will also
                // catch case-insensitive variations that aren't yet in our cache)
                bool dummy;
                SegmentHierarchyNode<TItem> node = FindDataNode(
                    UriSegmenter.ToPath(key.BaseAddress, hostNameComparisonMode, includePortInComparison), out dummy);
                if (node != null)
                {
                    item = node.Data;
                }
                // We want to cache both positive AND negative results
                AddToCache(key, item);
                return (item != null);
            }
        }

        public void RegisterUri(Uri uri, HostNameComparisonMode hostNameComparisonMode, TItem item)
        {
            Fx.Assert(HostNameComparisonModeHelper.IsDefined(hostNameComparisonMode), "RegisterUri: Invalid HostNameComparisonMode value passed in.");

            using (AsyncLock.TakeLock())
            {
                // Since every newly registered Uri could alter what Prefixes should have matched, we
                // should clear the cache of any existing results and start over
                ClearCache();
                BaseUriWithWildcard key = new BaseUriWithWildcard(uri, hostNameComparisonMode);
                SegmentHierarchyNode<TItem> node = FindOrCreateNode(key);
                if (node.Data != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                        SR.DuplicateRegistration, uri)));
                }
                node.SetData(item, key);
                count++;
            }
        }

        public void UnregisterUri(Uri uri, HostNameComparisonMode hostNameComparisonMode)
        {
            using (AsyncLock.TakeLock())
            {
                // Since every removed Uri could alter what Prefixes should have matched, we
                // should clear the cache of any existing results and start over
                ClearCache();
                string[] path = UriSegmenter.ToPath(uri, hostNameComparisonMode, includePortInComparison);
                // Never remove the root
                if (path.Length == 0)
                {
                    root.RemoveData();
                }
                else
                {
                    root.RemovePath(path, 0);
                }
                count--;
            }
        }

        SegmentHierarchyNode<TItem> FindDataNode(string[] path, out bool exactMatch)
        {
            Fx.Assert(path != null, "FindDataNode: path is null");

            exactMatch = false;
            SegmentHierarchyNode<TItem> current = root;
            SegmentHierarchyNode<TItem> result = null;
            for (int i = 0; i < path.Length; ++i)
            {
                SegmentHierarchyNode<TItem> next;
                if (!current.TryGetChild(path[i], out next))
                {
                    break;
                }
                else if (next.Data != null)
                {
                    result = next;
                    exactMatch = (i == path.Length - 1);
                }
                current = next;
            }
            return result;
        }

        SegmentHierarchyNode<TItem> FindOrCreateNode(BaseUriWithWildcard baseUri)
        {
            Fx.Assert(baseUri != null, "FindOrCreateNode: baseUri is null");

            string[] path = UriSegmenter.ToPath(baseUri.BaseAddress, baseUri.HostNameComparisonMode, includePortInComparison);
            SegmentHierarchyNode<TItem> current = root;
            for (int i = 0; i < path.Length; ++i)
            {
                SegmentHierarchyNode<TItem> next;
                if (!current.TryGetChild(path[i], out next))
                {
                    next = new SegmentHierarchyNode<TItem>(path[i], useWeakReferences);
                    current.SetChildNode(path[i], next);
                }
                current = next;
            }
            return current;
        }

        static class UriSegmenter
        {
            internal static string[] ToPath(Uri uriPath, HostNameComparisonMode hostNameComparisonMode,
                bool includePortInComparison)
            {
                if (null == uriPath)
                {
                    return new string[0];
                }
                UriSegmentEnum segmentEnum = new UriSegmentEnum(uriPath); // struct
                return segmentEnum.GetSegments(hostNameComparisonMode, includePortInComparison);
            }

            struct UriSegmentEnum
            {
                string segment;
                int segmentStartAt;
                int segmentLength;
                UriSegmentType type;
                Uri uri;

                internal UriSegmentEnum(Uri uri)
                {
                    Fx.Assert(null != uri, "UreSegmentEnum: null uri");
                    this.uri = uri;
                    type = UriSegmentType.Unknown;
                    segment = null;
                    segmentStartAt = 0;
                    segmentLength = 0;
                }

                void ClearSegment()
                {
                    type = UriSegmentType.None;
                    segment = string.Empty;
                    segmentStartAt = 0;
                    segmentLength = 0;
                }

                public string[] GetSegments(HostNameComparisonMode hostNameComparisonMode,
                    bool includePortInComparison)
                {
                    List<string> segments = new List<string>();
                    while (Next())
                    {
                        switch (type)
                        {
                            case UriSegmentType.Path:
                                segments.Add(segment.Substring(segmentStartAt, segmentLength));
                                break;

                            case UriSegmentType.Host:
                                if (hostNameComparisonMode == HostNameComparisonMode.StrongWildcard)
                                {
                                    segments.Add("+");
                                }
                                else if (hostNameComparisonMode == HostNameComparisonMode.Exact)
                                {
                                    segments.Add(segment);
                                }
                                else
                                {
                                    segments.Add("*");
                                }
                                break;

                            case UriSegmentType.Port:
                                if (includePortInComparison || hostNameComparisonMode == HostNameComparisonMode.Exact)
                                {
                                    segments.Add(segment);
                                }
                                break;

                            default:
                                segments.Add(segment);
                                break;
                        }
                    }
                    return segments.ToArray();
                }

                public bool Next()
                {
                    while (true)
                    {
                        switch (type)
                        {
                            case UriSegmentType.Unknown:
                                type = UriSegmentType.Scheme;
                                SetSegment(uri.Scheme);
                                return true;

                            case UriSegmentType.Scheme:
                                type = UriSegmentType.Host;
                                string host = uri.Host;
                                // The userName+password also accompany...
                                string userInfo = uri.UserInfo;
                                if (null != userInfo && userInfo.Length > 0)
                                {
                                    host = userInfo + '@' + host;
                                }
                                SetSegment(host);
                                return true;

                            case UriSegmentType.Host:
                                type = UriSegmentType.Port;
                                int port = uri.Port;
                                SetSegment(port.ToString(CultureInfo.InvariantCulture));
                                return true;

                            case UriSegmentType.Port:
                                type = UriSegmentType.Path;
                                string absPath = uri.AbsolutePath;
                                Fx.Assert(null != absPath, "Next: nill absPath");
                                if (0 == absPath.Length)
                                {
                                    ClearSegment();
                                    return false;
                                }
                                segment = absPath;
                                segmentStartAt = 0;
                                segmentLength = 0;
                                return NextPathSegment();

                            case UriSegmentType.Path:
                                return NextPathSegment();

                            case UriSegmentType.None:
                                return false;

                            default:
                                Fx.Assert("Next: unknown enum value");
                                return false;
                        }
                    }
                }

                public bool NextPathSegment()
                {
                    segmentStartAt += segmentLength;
                    while (segmentStartAt < segment.Length && segment[segmentStartAt] == '/')
                    {
                        segmentStartAt++;
                    }

                    if (segmentStartAt < segment.Length)
                    {
                        int next = segment.IndexOf('/', segmentStartAt);
                        if (-1 == next)
                        {
                            segmentLength = segment.Length - segmentStartAt;
                        }
                        else
                        {
                            segmentLength = next - segmentStartAt;
                        }
                        return true;
                    }
                    ClearSegment();
                    return false;
                }

                void SetSegment(string segment)
                {
                    this.segment = segment;
                    segmentStartAt = 0;
                    segmentLength = segment.Length;
                }

                enum UriSegmentType
                {
                    Unknown,
                    Scheme,
                    Host,
                    Port,
                    Path,
                    None
                }
            }
        }
    }

    class SegmentHierarchyNode<TData>
        where TData : class
    {
        BaseUriWithWildcard path;
        TData data;
        string name;
        Dictionary<string, SegmentHierarchyNode<TData>> children;
        WeakReference weakData;
        bool useWeakReferences;

        public SegmentHierarchyNode(string name, bool useWeakReferences)
        {
            this.name = name;
            this.useWeakReferences = useWeakReferences;
            children = new Dictionary<string, SegmentHierarchyNode<TData>>(StringComparer.OrdinalIgnoreCase);
        }

        public TData Data
        {
            get
            {
                if (useWeakReferences)
                {
                    if (weakData == null)
                    {
                        return null;
                    }
                    else
                    {
                        return weakData.Target as TData;
                    }
                }
                else
                {
                    return data;
                }
            }
        }

        public void SetData(TData data, BaseUriWithWildcard path)
        {
            this.path = path;
            if (useWeakReferences)
            {
                if (data == null)
                {
                    weakData = null;
                }
                else
                {
                    weakData = new WeakReference(data);
                }
            }
            else
            {
                this.data = data;
            }
        }

        public void SetChildNode(string name, SegmentHierarchyNode<TData> node)
        {
            children[name] = node;
        }

        public void Collect(List<KeyValuePair<BaseUriWithWildcard, TData>> result)
        {
            TData localData = Data;
            if (localData != null)
            {
                result.Add(new KeyValuePair<BaseUriWithWildcard, TData>(path, localData));
            }

            foreach (SegmentHierarchyNode<TData> child in children.Values)
            {
                child.Collect(result);
            }
        }

        public bool TryGetChild(string segment, out SegmentHierarchyNode<TData> value)
        {
            return children.TryGetValue(segment, out value);
        }

        public void RemoveData()
        {
            SetData(null, null);
        }

        // bool is whether to remove this node
        public bool RemovePath(string[] path, int seg)
        {
            if (seg == path.Length)
            {
                RemoveData();
                return children.Count == 0;
            }

            SegmentHierarchyNode<TData> node;
            if (!TryGetChild(path[seg], out node))
            {
                return (children.Count == 0 && Data == null);
            }

            if (node.RemovePath(path, seg + 1))
            {
                children.Remove(path[seg]);
                return (children.Count == 0 && Data == null);
            }
            else
            {
                return false;
            }
        }
    }

}