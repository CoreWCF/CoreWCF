// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using ServiceContract;

namespace Services
{
    public class CollectionsService : ICollectionsService
    {
        public Dictionary<string, string> EchoDictionary(Dictionary<string, string> echo) => throw new NotImplementedException();
        public IDictionary<string, string> EchoIDictionary(IDictionary<string, string> echo) => throw new NotImplementedException();
        public string[] EchoStringArray(string[] echo) => throw new NotImplementedException();
        public IEnumerable<string> EchoStringEnumerable(IEnumerable<string> echo) => throw new NotImplementedException();
        public List<string> EchoStringList(List<string> echo) => throw new NotImplementedException();
    }
}
