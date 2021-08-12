// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    [ServiceContract(Namespace = "http://microsoft.samples", Name = "ICollectionsTest")]
    public interface ITaskCollectionsTest
    {
        [OperationContract]
        Task<LinkedList<int>> GetList();

        [OperationContract]
        Task<Dictionary<string, int>> GetDictionary();

        [OperationContract]
        Task<HashSet<Book>> GetSet();

        [OperationContract]
        Task<Stack<byte>> GetStack();

        [OperationContract]
        Task<Queue<string>> GetQueue();
    }

    [DataContract]
    public class Book
    {
        [DataMember]
        public string Name;

        [DataMember]
        public Guid ISBN;

        [DataMember]
        public string Publisher;
    }
}


