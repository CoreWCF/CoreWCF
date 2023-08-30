// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "CollectionOfKeyValuePairDataService")]
    public interface ICollectionOfKeyValuePairDataService
    {
        [OperationContract]
        KeyValueContainingArray EchoKeyValueWithArray(KeyValueContainingArray echo);

        [OperationContract]
        KeyValueContainingList EchoKeyValueWithList(KeyValueContainingList echo);
    }

    [DataContract]
    public class KeyValueContainingArray
    {
        [DataMember]
        public KeyValuePair<string, int>[] KeyValueArray { get; set; }
    }

    [DataContract]
    public class KeyValueContainingList
    {
        [DataMember]
        public List<KeyValuePair<string, int>> KeyValueList { get; set; }
    }
}
