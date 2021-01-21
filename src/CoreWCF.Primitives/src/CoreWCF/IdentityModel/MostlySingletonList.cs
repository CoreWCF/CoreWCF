// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.IdentityModel
{
    internal struct MostlySingletonList<T> where T : class
    {
        private T singleton;
        private List<T> list;

        public T this[int index]
        {
            get
            {
                if (list == null)
                {
                    EnsureValidSingletonIndex(index);
                    return singleton;
                }
                else
                {
                    return list[index];
                }
            }
        }

        public int Count { get; private set; }

        public void Add(T item)
        {
            if (list == null)
            {
                if (Count == 0)
                {
                    singleton = item;
                    Count = 1;
                    return;
                }
                list = new List<T>();
                list.Add(singleton);
                singleton = null;
            }
            list.Add(item);
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
            return Count == 1 && Compare(singleton, item);
        }

        public int IndexOf(T item)
        {
            if (list == null)
            {
                return MatchesSingleton(item) ? 0 : -1;
            }
            else
            {
                return list.IndexOf(item);
            }
        }

        public bool Remove(T item)
        {
            if (list == null)
            {
                if (MatchesSingleton(item))
                {
                    singleton = null;
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
                bool result = list.Remove(item);
                if (result)
                {
                    Count--;
                }
                return result;
            }
        }

        public void RemoveAt(int index)
        {
            if (list == null)
            {
                EnsureValidSingletonIndex(index);
                singleton = null;
                Count = 0;
            }
            else
            {
                list.RemoveAt(index);
                Count--;
            }
        }
    }
}
