// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace CoreWCF.DataContractSerialization.Generator;

/// <summary>
/// An array with structural equality, for use inside incremental generator models.
/// </summary>
/// <remarks>
/// Roslyn caches pipeline steps by comparing their outputs, and both <see cref="Array"/> and
/// <c>ImmutableArray&lt;T&gt;</c> compare by reference - so a model containing one is recomputed on
/// every keystroke no matter how little changed. Wrapping the elements in a type that compares by
/// value is what makes the cache actually work.
/// </remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[]? items) => _items = items;

    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    public int Count => _items?.Length ?? 0;

    public T this[int index] => _items![index];

    /// <summary>A copy with one more item appended.</summary>
    public EquatableArray<T> Add(T item)
    {
        T[] items = new T[Count + 1];
        for (int i = 0; i < Count; i++)
        {
            items[i] = _items![i];
        }

        items[Count] = item;
        return new EquatableArray<T>(items);
    }

    public bool Equals(EquatableArray<T> other)
    {
        if (ReferenceEquals(_items, other._items))
        {
            return true;
        }

        if (_items is null || other._items is null || _items.Length != other._items.Length)
        {
            return false;
        }

        for (int i = 0; i < _items.Length; i++)
        {
            if (!_items[i].Equals(other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_items is null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            foreach (T item in _items)
            {
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (T item in _items ?? Array.Empty<T>())
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
