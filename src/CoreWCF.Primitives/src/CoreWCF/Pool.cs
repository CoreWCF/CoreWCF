// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal class Pool<T> where T : class
    {
        private readonly T[] items;

        public Pool(int maxCount)
        {
            items = new T[maxCount];
        }

        public int Count { get; private set; }

        public T Take()
        {
            if (Count > 0)
            {
                T item = items[--Count];
                items[Count] = null;
                return item;
            }
            else
            {
                return null;
            }
        }

        public bool Return(T item)
        {
            if (Count < items.Length)
            {
                items[Count++] = item;
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
                items[i] = null;
            }

            Count = 0;
        }
    }
}