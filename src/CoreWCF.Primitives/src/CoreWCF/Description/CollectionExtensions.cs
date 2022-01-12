// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    internal static class CollectionExtensions
    {
        public static Collection<T2> FindAll<T, T2>(this KeyedCollection<Type, T> keyedCollection)
        {
            if (keyedCollection is KeyedByTypeCollection<T> keyedByTypeCollection)
            {
                return keyedByTypeCollection.FindAll<T2>();
            }

            Fx.Assert($"Collection must be of type KeyedByTypeCollection<{typeof(T).Name}>");
            throw new ArgumentException($"Collection must be of type KeyedByTypeCollection<{typeof(T).Name}>", nameof(keyedCollection));
        }
    }
}
