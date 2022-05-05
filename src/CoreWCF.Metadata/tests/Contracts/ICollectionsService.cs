// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "CollectionsService")]
    public interface ICollectionsService
    {
        [OperationContract]
        List<string> EchoStringList(List<string> echo);

        [OperationContract]
        IEnumerable<string> EchoStringEnumerable(IEnumerable<string> echo);

        [OperationContract]
        string[] EchoStringArray(string[] echo);

        [OperationContract]
        Dictionary<string, string> EchoDictionary(Dictionary<string, string> echo);

        [OperationContract]
        IDictionary<string, string> EchoIDictionary(IDictionary<string, string> echo);
    }
}
