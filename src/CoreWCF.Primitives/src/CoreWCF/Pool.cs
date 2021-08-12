// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal class Pool<T> where T : class
    {
        private readonly T[] _items;

        public Pool(int maxCount)
        {
            _items = new T[maxCount];
        }

        public int Count { get; private set; }

        public T Take()
        {
            if (Count > 0)
            {
                T item = _items[--Count];
                _items[Count] = null;
                return item;
            }
            else
            {
                return null;
            }
        }

        public bool Return(T item)
        {
            if (Count < _items.Length)
            {
                _items[Count++] = item;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++)
            {
                _items[i] = null;
            }

            Count = 0;
        }
    }
}