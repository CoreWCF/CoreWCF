// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public interface IExtensibleObject<T> where T : IExtensibleObject<T>
    {
        IExtensionCollection<T> Extensions { get; }
    }
}