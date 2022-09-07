// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ServiceContract;

namespace Services
{
    public class CollectionOfKeyValuePairDataService : ICollectionOfKeyValuePairDataService
    {
        public KeyValueContainingArray EchoKeyValueWithArray(KeyValueContainingArray echo) => throw new NotImplementedException();
        public KeyValueContainingList EchoKeyValueWithList(KeyValueContainingList echo) => throw new NotImplementedException();
    }
}
