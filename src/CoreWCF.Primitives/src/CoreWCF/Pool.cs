// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal class Pool<T> where T : class
    {
        private T[] items;
        private int count;

        public Pool(int maxCount)
        {
            items = new T[maxCount];
        }

        public int Count
        {
            get { return count; }
        }

        public T Take()
        {
            if (count > 0)
            {
                T item = items[--count];
                items[count] = null;
                return item;
            }
            else
            {
                return null;
            }
        }

        public bool Return(T item)
        {
            if (count < items.Length)
            {
                items[count++] = item;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < count; i++)
                items[i] = null;
            count = 0;
        }
    }
}