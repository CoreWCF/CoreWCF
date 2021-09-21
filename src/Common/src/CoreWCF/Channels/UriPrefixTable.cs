// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Collections;

namespace CoreWCF.Channels
{
    internal sealed class UriPrefixTable<TItem> : IEnumerable<KeyValuePair<BaseUriWithWildcard, TItem>>
        where TItem : class
    {
        private const int HopperSize = 128;
        private volatile HopperCache _lookupCache; // cache matches, for lookup speed
        private readonly SegmentHierarchyNode<TItem> _root;
        private readonly bool _useWeakReferences;
        private readonly bool _includePortInComparison;

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
            _includePortInComparison = includePortInComparison;
            _useWeakReferences = useWeakReferences;
            _root = new SegmentHierarchyNode<TItem>(null, useWeakReferences);
            _lookupCache = new HopperCache(HopperSize, useWeakReferences);
        }

        internal UriPrefixTable(UriPrefixTable<TItem> objectToClone)
            : this(objectToClone._includePortInComparison, objectToClone._useWeakReferences)
        {
            if (objectToClone.Count > 0)
            {
                foreach (KeyValuePair<BaseUriWithWildcard, TItem> current in objectToClone.GetAll())
                {
                    RegisterUri(current.Key.BaseAddress, current.Key.HostNameComparisonMode, current.Value);
                }
            }
        }

        private object ThisLock
        {
            get
            {
                // The UriPrefixTable instance itself is used as a
                // synchronization primitive in the TransportManagers and the
                // TransportManagerContainers so we return 'this' to keep them in sync.
                return this;
            }
        }

        public int Count { get; private set; }

        public AsyncLock AsyncLock { get; } = new AsyncLock();

        public bool IsRegistered(BaseUriWithWildcard key)
        {
            Uri uri = key.BaseAddress;

            // don't need to normalize path since SegmentHierarchyNode is
            // already OrdinalIgnoreCase
            string[] paths = UriSegmenter.ToPath(uri, key.HostNameComparisonMode, _includePortInComparison);
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
                _root.Collect(result);
                return result;
            }
        }

        private bool TryCacheLookup(BaseUriWithWildcard key, out TItem item)
        {
            object value = _lookupCache.GetValue(ThisLock, key);

            // We might return null and true in the case of DBNull (cached negative result).
            // When TItem is object, the cast isn't sufficient to weed out DBNulls, so we need an explicit check.
            item = value == DBNull.Value ? null : (TItem)value;
            return value != null;
        }

        private void AddToCache(BaseUriWithWildcard key, TItem item)
        {
            // Don't allow explicitly adding DBNulls.
            Fx.Assert(item != DBNull.Value, "Can't add DBNull to UriPrefixTable.");

            // HopperCache uses null as 'doesn't exist', so use DBNull as a stand-in for null.
            _lookupCache.Add(key, item ?? (object)DBNull.Value);
        }

        private void ClearCache()
        {
            _lookupCache = new HopperCache(HopperSize, _useWeakReferences);
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
                SegmentHierarchyNode<TItem> node = FindDataNode(
                    UriSegmenter.ToPath(key.BaseAddress, hostNameComparisonMode, _includePortInComparison), out bool dummy);
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SRCommon.Format(
                        SRCommon.DuplicateRegistration, uri)));
                }
                node.SetData(item, key);
                Count++;
            }
        }

        public void UnregisterUri(Uri uri, HostNameComparisonMode hostNameComparisonMode)
        {
            using (AsyncLock.TakeLock())
            {
                // Since every removed Uri could alter what Prefixes should have matched, we
                // should clear the cache of any existing results and start over
                ClearCache();
                string[] path = UriSegmenter.ToPath(uri, hostNameComparisonMode, _includePortInComparison);
                // Never remove the root
                if (path.Length == 0)
                {
                    _root.RemoveData();
                }
                else
                {
                    _root.RemovePath(path, 0);
                }
                Count--;
            }
        }

        private SegmentHierarchyNode<TItem> FindDataNode(string[] path, out bool exactMatch)
        {
            Fx.Assert(path != null, "FindDataNode: path is null");

            exactMatch = false;
            SegmentHierarchyNode<TItem> current = _root;
            SegmentHierarchyNode<TItem> result = null;
            for (int i = 0; i < path.Length; ++i)
            {
                if (!current.TryGetChild(path[i], out SegmentHierarchyNode<TItem> next))
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

        private SegmentHierarchyNode<TItem> FindOrCreateNode(BaseUriWithWildcard baseUri)
        {
            Fx.Assert(baseUri != null, "FindOrCreateNode: baseUri is null");

            string[] path = UriSegmenter.ToPath(baseUri.BaseAddress, baseUri.HostNameComparisonMode, _includePortInComparison);
            SegmentHierarchyNode<TItem> current = _root;
            for (int i = 0; i < path.Length; ++i)
            {
                if (!current.TryGetChild(path[i], out SegmentHierarchyNode<TItem> next))
                {
                    next = new SegmentHierarchyNode<TItem>(path[i], _useWeakReferences);
                    current.SetChildNode(path[i], next);
                }
                current = next;
            }
            return current;
        }

        public IEnumerator<KeyValuePair<BaseUriWithWildcard, TItem>> GetEnumerator() => GetAll().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetAll().GetEnumerator();

        private static class UriSegmenter
        {
            internal static string[] ToPath(Uri uriPath, HostNameComparisonMode hostNameComparisonMode,
                bool includePortInComparison)
            {
                if (null == uriPath)
                {
                    return Array.Empty<string>();
                }
                UriSegmentEnum segmentEnum = new UriSegmentEnum(uriPath); // struct
                return segmentEnum.GetSegments(hostNameComparisonMode, includePortInComparison);
            }

            private struct UriSegmentEnum
            {
                private string _segment;
                private int _segmentStartAt;
                private int _segmentLength;
                private UriSegmentType _type;
                private readonly Uri _uri;

                internal UriSegmentEnum(Uri uri)
                {
                    Fx.Assert(null != uri, "UreSegmentEnum: null uri");
                    _uri = uri;
                    _type = UriSegmentType.Unknown;
                    _segment = null;
                    _segmentStartAt = 0;
                    _segmentLength = 0;
                }

                private void ClearSegment()
                {
                    _type = UriSegmentType.None;
                    _segment = string.Empty;
                    _segmentStartAt = 0;
                    _segmentLength = 0;
                }

                public string[] GetSegments(HostNameComparisonMode hostNameComparisonMode,
                    bool includePortInComparison)
                {
                    List<string> segments = new List<string>();
                    while (Next())
                    {
                        switch (_type)
                        {
                            case UriSegmentType.Path:
                                segments.Add(_segment.Substring(_segmentStartAt, _segmentLength));
                                break;

                            case UriSegmentType.Host:
                                if (hostNameComparisonMode == HostNameComparisonMode.StrongWildcard)
                                {
                                    segments.Add("+");
                                }
                                else if (hostNameComparisonMode == HostNameComparisonMode.Exact)
                                {
                                    segments.Add(_segment);
                                }
                                else
                                {
                                    segments.Add("*");
                                }
                                break;

                            case UriSegmentType.Port:
                                if (includePortInComparison || hostNameComparisonMode == HostNameComparisonMode.Exact)
                                {
                                    segments.Add(_segment);
                                }
                                break;

                            default:
                                segments.Add(_segment);
                                break;
                        }
                    }
                    return segments.ToArray();
                }

                public bool Next()
                {
                    while (true)
                    {
                        switch (_type)
                        {
                            case UriSegmentType.Unknown:
                                _type = UriSegmentType.Scheme;
                                SetSegment(_uri.Scheme);
                                return true;

                            case UriSegmentType.Scheme:
                                _type = UriSegmentType.Host;
                                string host = _uri.Host;
                                // The userName+password also accompany...
                                string userInfo = _uri.UserInfo;
                                if (null != userInfo && userInfo.Length > 0)
                                {
                                    host = userInfo + '@' + host;
                                }
                                SetSegment(host);
                                return true;

                            case UriSegmentType.Host:
                                _type = UriSegmentType.Port;
                                int port = _uri.Port;
                                SetSegment(port.ToString(CultureInfo.InvariantCulture));
                                return true;

                            case UriSegmentType.Port:
                                _type = UriSegmentType.Path;
                                string absPath = _uri.AbsolutePath;
                                Fx.Assert(null != absPath, "Next: nill absPath");
                                if (0 == absPath.Length)
                                {
                                    ClearSegment();
                                    return false;
                                }
                                _segment = absPath;
                                _segmentStartAt = 0;
                                _segmentLength = 0;
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
                    _segmentStartAt += _segmentLength;
                    while (_segmentStartAt < _segment.Length && _segment[_segmentStartAt] == '/')
                    {
                        _segmentStartAt++;
                    }

                    if (_segmentStartAt < _segment.Length)
                    {
                        int next = _segment.IndexOf('/', _segmentStartAt);
                        if (-1 == next)
                        {
                            _segmentLength = _segment.Length - _segmentStartAt;
                        }
                        else
                        {
                            _segmentLength = next - _segmentStartAt;
                        }
                        return true;
                    }
                    ClearSegment();
                    return false;
                }

                private void SetSegment(string segment)
                {
                    _segment = segment;
                    _segmentStartAt = 0;
                    _segmentLength = segment.Length;
                }

                private enum UriSegmentType
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

    internal class SegmentHierarchyNode<TData>
        where TData : class
    {
        private BaseUriWithWildcard _path;
        private TData _data;
        private readonly string _name;
        private readonly Dictionary<string, SegmentHierarchyNode<TData>> _children;
        private WeakReference _weakData;
        private readonly bool _useWeakReferences;

        public SegmentHierarchyNode(string name, bool useWeakReferences)
        {
            _name = name;
            _useWeakReferences = useWeakReferences;
            _children = new Dictionary<string, SegmentHierarchyNode<TData>>(StringComparer.OrdinalIgnoreCase);
        }

        public TData Data
        {
            get
            {
                if (_useWeakReferences)
                {
                    if (_weakData == null)
                    {
                        return null;
                    }
                    else
                    {
                        return _weakData.Target as TData;
                    }
                }
                else
                {
                    return _data;
                }
            }
        }

        public void SetData(TData data, BaseUriWithWildcard path)
        {
            _path = path;
            if (_useWeakReferences)
            {
                if (data == null)
                {
                    _weakData = null;
                }
                else
                {
                    _weakData = new WeakReference(data);
                }
            }
            else
            {
                _data = data;
            }
        }

        public void SetChildNode(string name, SegmentHierarchyNode<TData> node)
        {
            _children[name] = node;
        }

        public void Collect(List<KeyValuePair<BaseUriWithWildcard, TData>> result)
        {
            TData localData = Data;
            if (localData != null)
            {
                result.Add(new KeyValuePair<BaseUriWithWildcard, TData>(_path, localData));
            }

            foreach (SegmentHierarchyNode<TData> child in _children.Values)
            {
                child.Collect(result);
            }
        }

        public bool TryGetChild(string segment, out SegmentHierarchyNode<TData> value)
        {
            return _children.TryGetValue(segment, out value);
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
                return _children.Count == 0;
            }

            if (!TryGetChild(path[seg], out SegmentHierarchyNode<TData> node))
            {
                return (_children.Count == 0 && Data == null);
            }

            if (node.RemovePath(path, seg + 1))
            {
                _children.Remove(path[seg]);
                return (_children.Count == 0 && Data == null);
            }
            else
            {
                return false;
            }
        }
    }
}
