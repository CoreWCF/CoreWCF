// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.IdentityModel
{
    internal struct MostlySingletonList<T> where T : class
    {
        private T _singleton;
        private List<T> _list;

        public T this[int index]
        {
            get
            {
                if (_list == null)
                {
                    EnsureValidSingletonIndex(index);
                    return _singleton;
                }
                else
                {
                    return _list[index];
                }
            }
        }

        public int Count { get; private set; }

        public void Add(T item)
        {
            if (_list == null)
            {
                if (Count == 0)
                {
                    _singleton = item;
                    Count = 1;
                    return;
                }
                _list = new List<T>();
                _list.Add(_singleton);
                _singleton = null;
            }
            _list.Add(item);
            Count++;
        }

        private static bool Compare(T x, T y)
        {
            return x == null ? y == null : x.Equals(y);
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        private void EnsureValidSingletonIndex(int index)
        {
            if (Count != 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("count", SR.Format("ValueMustBeOne")));
            }

            if (index != 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("index", SR.Format("ValueMustBeZero")));
            }
        }

        private bool MatchesSingleton(T item)
        {
            return Count == 1 && Compare(_singleton, item);
        }

        public int IndexOf(T item)
        {
            if (_list == null)
            {
                return MatchesSingleton(item) ? 0 : -1;
            }
            else
            {
                return _list.IndexOf(item);
            }
        }

        public bool Remove(T item)
        {
            if (_list == null)
            {
                if (MatchesSingleton(item))
                {
                    _singleton = null;
                    Count = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                bool result = _list.Remove(item);
                if (result)
                {
                    Count--;
                }
                return result;
            }
        }

        public void RemoveAt(int index)
        {
            if (_list == null)
            {
                EnsureValidSingletonIndex(index);
                _singleton = null;
                Count = 0;
            }
            else
            {
                _list.RemoveAt(index);
                Count--;
            }
        }
    }
}
