// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.IdentityModel
{
    internal struct MostlySingletonList<T> where T : class
    {
        private int count;
        private T singleton;
        private List<T> list;

        public T this[int index]
        {
            get
            {
                if (this.list == null)
                {
                    EnsureValidSingletonIndex(index);
                    return this.singleton;
                }
                else
                {
                    return this.list[index];
                }
            }
        }

        public int Count
        {
            get { return this.count; }
        }

        public void Add(T item)
        {
            if (this.list == null)
            {
                if (this.count == 0)
                {
                    this.singleton = item;
                    this.count = 1;
                    return;
                }
                this.list = new List<T>();
                this.list.Add(this.singleton);
                this.singleton = null;
            }
            this.list.Add(item);
            this.count++;
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
            if (this.count != 1)
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
            return this.count == 1 && Compare(this.singleton, item);
        }

        public int IndexOf(T item)
        {
            if (this.list == null)
            {
                return MatchesSingleton(item) ? 0 : -1;
            }
            else
            {
                return this.list.IndexOf(item);
            }
        }

        public bool Remove(T item)
        {
            if (this.list == null)
            {
                if (MatchesSingleton(item))
                {
                    this.singleton = null;
                    this.count = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                bool result = this.list.Remove(item);
                if (result)
                {
                    this.count--;
                }
                return result;
            }
        }

        public void RemoveAt(int index)
        {
            if (this.list == null)
            {
                EnsureValidSingletonIndex(index);
                this.singleton = null;
                this.count = 0;
            }
            else
            {
                this.list.RemoveAt(index);
                this.count--;
            }
        }
    }
}
