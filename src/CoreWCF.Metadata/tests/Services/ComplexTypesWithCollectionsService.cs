// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ServiceContract;

namespace Services
{
    public class ComplexTypesWithCollectionsService : IComplexTypesWithCollectionsService
    {
        public DataContainingArray EchoComplexTypeWithArray(DataContainingArray echo) => throw new NotImplementedException();
        public DataContainingList EchoComplexTypeWithList(DataContainingList echo) => throw new NotImplementedException();
    }
}
