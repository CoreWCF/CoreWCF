// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public interface IExtensionCollection<T> : System.Collections.Generic.ICollection<IExtension<T>> where T : IExtensibleObject<T>
    {
        E Find<E>();
        System.Collections.ObjectModel.Collection<E> FindAll<E>();
    }
}