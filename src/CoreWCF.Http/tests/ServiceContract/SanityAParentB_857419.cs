// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface ISanityAParentB_857419_ContractDerived : ISanityAParentB_857419_ContractBase
    {
        [OperationContract(IsOneWay = true)]
        void OneWayMethod(object o);

        [OperationContract(IsOneWay = false)]
        string StringMethod(string s);

        [OperationContract(Name = "DerivedMethod")]
        new string Method(string input);
    }

    [ServiceContract]
    public interface ISanityAParentB_857419_ContractBase
    {
        [OperationContract(IsOneWay = false)]
        string TwoWayMethod(string input);

        [OperationContract(IsOneWay = false)]
        [ServiceKnownType(typeof(MyBaseDataType))]
        object DataContractMethod(object o);

        [OperationContract(Name = "BaseMethod")]
        string Method(string input);
    }

    [DataContract]
    public class MyBaseDataType
    {
        [DataMember]
        public string data = null;
    }
}