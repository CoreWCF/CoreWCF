// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    //
    // Generic struct representing ranges within buffers
    //
    internal struct QueryRange
    {
        internal int end;       // INCLUSIVE - the end of the range
        internal int start;     // INCLUSIVE - the start of the range
        internal QueryRange(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        internal int Count
        {
            get
            {
                return end - start + 1;
            }
        }
        internal bool IsInRange(int point)
        {
            return (start <= point && point <= end);
        }
        internal void Shift(int offset)
        {
            start += offset;
            end += offset;
        }
    }

    /// <summary>
    /// Our own buffer management
    /// There are a few reasons why we don't reuse something in System.Collections.Generic
    ///  1. We want Clear() to NOT reallocate the internal array. We want it to simply set the Count = 0
    ///     This allows us to reuse buffers with impunity.
    ///  2. We want to be able to replace the internal buffer in a collection with a different one. Again,
    ///     this is to help with pooling
    ///  3. We want to be able to control how fast buffers grow. 
    ///  4. Does absolutely no bounds or null checking. As fast as we can make it. All checking should be done
    ///  by whoever wraps this. Checking is unnecessary for many internal uses where we need optimal perf.
    ///  5. Does more precise trimming
    ///  6. AND this is a struct
    ///
    /// </summary>        
    internal struct QueryBuffer<T>
    {
        internal T[] buffer;    // buffer of T. Frequently larger than count
        internal int count;     // Actual # of items
        internal static T[] EmptyBuffer = Array.Empty<T>();

        /// <summary>
        /// Construct a new buffer
        /// </summary>
        /// <param name="capacity"></param>
        internal QueryBuffer(int capacity)
        {
            if (0 == capacity)
            {
                buffer = EmptyBuffer;
            }
            else
            {
                buffer = new T[capacity];
            }
            count = 0;
        }
        /// <summary>
        /// # of items
        /// </summary>
        internal int Count
        {
            get
            {
                return count;
            }
        }


        internal T this[int index]
        {
            get
            {
                return buffer[index];
            }
            set
            {
                buffer[index] = value;
            }
        }


        /// <summary>
        /// Add an element to the buffer
        /// </summary>
        internal void Add(T t)
        {
            if (count == buffer.Length)
            {
                Array.Resize<T>(ref buffer, count > 0 ? count * 2 : 16);
            }
            buffer[count++] = t;
        }


        /// <summary>
        /// Add all the elements in the given buffer to this one
        /// We can do this very efficiently using an Array Copy
        /// </summary>
        internal void Add(ref QueryBuffer<T> addBuffer)
        {
            if (1 == addBuffer.count)
            {
                Add(addBuffer.buffer[0]);
                return;
            }

            int newCount = count + addBuffer.count;
            if (newCount >= buffer.Length)
            {
                Grow(newCount);
            }
            // Copy all the new elements in
            Array.Copy(addBuffer.buffer, 0, buffer, count, addBuffer.count);
            count = newCount;
        }


        /// <summary>
        /// Set the count to zero but do NOT get rid of the actual buffer
        /// </summary>
        internal void Clear()
        {
            count = 0;
        }


        internal void CopyFrom(ref QueryBuffer<T> addBuffer)
        {
            int addCount = addBuffer.count;
            switch (addCount)
            {
                default:
                    if (addCount > buffer.Length)
                    {
                        buffer = new T[addCount];
                    }
                    // Copy all the new elements in
                    Array.Copy(addBuffer.buffer, 0, buffer, 0, addCount);
                    count = addCount;
                    break;

                case 0:
                    count = 0;
                    break;

                case 1:
                    if (buffer.Length == 0)
                    {
                        buffer = new T[1];
                    }
                    buffer[0] = addBuffer.buffer[0];
                    count = 1;
                    break;
            }
        }

        internal void CopyTo(T[] dest)
        {
            Array.Copy(buffer, dest, count);
        }

        private void Grow(int capacity)
        {
            int newCapacity = buffer.Length * 2;
            Array.Resize<T>(ref buffer, capacity > newCapacity ? capacity : newCapacity);
        }

        internal int IndexOf(T t)
        {
            for (int i = 0; i < count; ++i)
            {
                if (t.Equals(buffer[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        internal int IndexOf(T t, int startAt)
        {
            for (int i = startAt; i < count; ++i)
            {
                if (t.Equals(buffer[i]))
                {
                    return i;
                }
            }
            return -1;
        }
        internal bool IsValidIndex(int index)
        {
            return (index >= 0 && index < count);
        }

        /// <summary>
        /// Reserve enough space for count elements
        /// </summary>
        internal void Reserve(int reserveCount)
        {
            int newCount = count + reserveCount;
            if (newCount >= buffer.Length)
            {
                Grow(newCount);
            }
            count = newCount;
        }

        internal void ReserveAt(int index, int reserveCount)
        {
            if (index == count)
            {
                Reserve(reserveCount);
                return;
            }

            int newCount;
            if (index > count)
            {
                // We want to reserve starting at a location past what is current committed. 
                // No shifting needed
                newCount = index + reserveCount + 1;
                if (newCount >= buffer.Length)
                {
                    Grow(newCount);
                }
            }
            else
            {
                // reserving space within an already allocated portion of the buffer
                // we'll ensure that the buffer can fit 'newCount' items, then shift by reserveCount starting at index
                newCount = count + reserveCount;
                if (newCount >= buffer.Length)
                {
                    Grow(newCount);
                }
                // Move to make room
                Array.Copy(buffer, index, buffer, index + reserveCount, count - index);
            }
            count = newCount;
        }

        internal void Remove(T t)
        {
            int index = IndexOf(t);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }

        internal void RemoveAt(int index)
        {
            if (index < count - 1)
            {
                Array.Copy(buffer, index + 1, buffer, index, count - index - 1);
            }
            count--;
        }

        internal void Sort(IComparer<T> comparer)
        {
            Array.Sort<T>(buffer, 0, count, comparer);
        }

        /// <summary>
        /// Reduce the buffer capacity so that its size is exactly == to the element count
        /// </summary>
        internal void TrimToCount()
        {
            if (count < buffer.Length)
            {
                if (0 == count)
                {
                    buffer = EmptyBuffer;
                }
                else
                {
                    T[] newBuffer = new T[count];
                    Array.Copy(buffer, newBuffer, count);
                }
            }
        }
    }

    internal struct SortedBuffer<T, C>
            where C : IComparer<T>
    {
        private T[] _buffer;
        private static DefaultComparer s_comparer;

        internal SortedBuffer(C comparerInstance)
        {
            Count = 0;
            _buffer = null;

            if (s_comparer == null)
            {
                s_comparer = new DefaultComparer(comparerInstance);
            }
            else
            {
                Fx.Assert(ReferenceEquals(DefaultComparer.Comparer, comparerInstance), "The SortedBuffer type has already been initialized with a different comparer instance.");
            }
        }

        internal T this[int index]
        {
            get
            {
                return GetAt(index);
            }
        }

        internal int Capacity
        {
            set
            {
                if (_buffer != null)
                {
                    if (value != _buffer.Length)
                    {
                        Fx.Assert(value >= Count, "New capacity must be >= size");
                        if (value > 0)
                        {
                            Array.Resize(ref _buffer, value);
                        }
                        else
                        {
                            _buffer = null;
                        }
                    }
                }
                else
                {
                    _buffer = new T[value];
                }
            }
        }

        internal int Count { get; private set; }

        internal int Add(T item)
        {
            int i = Search(item);

            if (i < 0)
            {
                i = ~i;
                InsertAt(i, item);
            }

            return i;
        }

        internal void Clear()
        {
            Count = 0;
        }
        internal void Exchange(T old, T replace)
        {
            if (s_comparer.Compare(old, replace) == 0)
            {
                int i = IndexOf(old);
                if (i >= 0)
                {
                    _buffer[i] = replace;
                }
                else
                {
                    Insert(replace);
                }
            }
            else
            {
                // PERF, astern, can this be made more efficient?  Does it need to be?
                Remove(old);
                Insert(replace);
            }
        }

        internal T GetAt(int index)
        {
            Fx.Assert(index < Count, "Index is greater than size");
            return _buffer[index];
        }

        internal int IndexOf(T item)
        {
            return Search(item);
        }

        internal int IndexOfKey<K>(K key, IItemComparer<K, T> itemComp)
        {
            return Search(key, itemComp);
        }

        internal int Insert(T item)
        {
            int i = Search(item);

            if (i >= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCritical(new ArgumentException(SR.QueryItemAlreadyExists));
            }

            // If an item is not found, Search returns the bitwise negation of
            // the index an item should inserted at;
            InsertAt(~i, item);
            return ~i;
        }

        private void InsertAt(int index, T item)
        {
            Fx.Assert(index >= 0 && index <= Count, "");

            if (_buffer == null)
            {
                _buffer = new T[1];
            }
            else if (_buffer.Length == Count)
            {
                // PERF, astern, how should we choose a new size?
                T[] tmp = new T[Count + 1];

                if (index == 0)
                {
                    Array.Copy(_buffer, 0, tmp, 1, Count);
                }
                else if (index == Count)
                {
                    Array.Copy(_buffer, 0, tmp, 0, Count);
                }
                else
                {
                    Array.Copy(_buffer, 0, tmp, 0, index);
                    Array.Copy(_buffer, index, tmp, index + 1, Count - index);
                }

                _buffer = tmp;
            }
            else
            {
                Array.Copy(_buffer, index, _buffer, index + 1, Count - index);
            }

            _buffer[index] = item;
            ++Count;
        }

        internal bool Remove(T item)
        {
            int i = IndexOf(item);

            if (i >= 0)
            {
                RemoveAt(i);
                return true;
            }

            return false;
        }

        internal void RemoveAt(int index)
        {
            Fx.Assert(index >= 0 && index < Count, "");

            if (index < Count - 1)
            {
                Array.Copy(_buffer, index + 1, _buffer, index, Count - index - 1);
            }

            _buffer[--Count] = default;
        }

        private int Search(T item)
        {
            if (Count == 0)
            {
                return ~0;
            }

            return Search(item, s_comparer);
        }

        private int Search<K>(K key, IItemComparer<K, T> comparer)
        {
            if (Count <= 8)
            {
                return LinearSearch<K>(key, comparer, 0, Count);
            }
            else
            {
                return BinarySearch(key, comparer);
            }
        }

        private int BinarySearch<K>(K key, IItemComparer<K, T> comparer)
        {
            // [low, high)
            int low = 0;
            int high = Count;
            int mid, result;

            // Binary search is implemented here so we could look for a type that is different from the
            // buffer type.  Also, the search switches to linear for 8 or fewer elements.
            while (high - low > 8)
            {
                mid = (high + low) / 2;
                result = comparer.Compare(key, _buffer[mid]);
                if (result < 0)
                {
                    high = mid;
                }
                else if (result > 0)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return LinearSearch<K>(key, comparer, low, high);
        }

        // [start, bound)
        private int LinearSearch<K>(K key, IItemComparer<K, T> comparer, int start, int bound)
        {
            int result;

            for (int i = start; i < bound; ++i)
            {
                result = comparer.Compare(key, _buffer[i]);
                if (result == 0)
                {
                    return i;
                }

                if (result < 0)
                {
                    // Return the bitwise negation of the insertion index
                    return ~i;
                }
            }

            // Return the bitwise negation of the insertion index
            return ~bound;
        }
        internal void Trim()
        {
            Capacity = Count;
        }

        internal class DefaultComparer : IItemComparer<T, T>
        {
            public static IComparer<T> Comparer;

            public DefaultComparer(C comparer)
            {
                Comparer = comparer;
            }

            public int Compare(T item1, T item2)
            {
                return Comparer.Compare(item1, item2);
            }
        }
    }

    internal interface IItemComparer<K, V>
    {
        int Compare(K key, V value);
    }
}