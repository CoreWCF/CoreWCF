// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = "ComplexTypesWithCollectionsService")]
    public interface IComplexTypesWithCollectionsService
    {
        [OperationContract]
        DataContainingList EchoComplexTypeWithList(DataContainingList echo);

        [OperationContract]
        DataContainingArray EchoComplexTypeWithArray(DataContainingArray echo);
    }

    [DataContract]
    public class DataContainingArray
    {
        [DataMember]
        public string[] StringDataArray { get; set; }
    }

    [DataContract]
    public class DataContainingList
    {
        [DataMember]
        public List<string> StringDataList { get; set; }
    }
}
