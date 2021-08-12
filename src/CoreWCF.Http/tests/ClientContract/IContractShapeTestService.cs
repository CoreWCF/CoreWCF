// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.ServiceModel;

namespace ClientContract
{
    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class SM_ComplexType
    {
        [DataMember]
        public int n;
        [DataMember]
        public string s;
    }

    [ServiceContract(Name = "IContractService")]
    internal interface IServiceContract_Overloads
    {
        [OperationContract(Name = "TwoWayInt")]
        string TwoWayMethod(int n);

        [OperationContract(Name = "TwoWayString")]
        string TwoWayMethod(string s);

        [OperationContract(Name = "TwoWayComplex")]
        string TwoWayMethod(SM_ComplexType ct);

        [OperationContract(Name = "TwoWayVoid")]
        string TwoWayMethod();
    }

    [ServiceContract(Name = "IContractService")]
    internal interface IServiceContract_Params
    {
        // Two Way w/ Parameter Array
        [OperationContract]
        string TwoWayParamArray(int n, params int[] args);
    }
}