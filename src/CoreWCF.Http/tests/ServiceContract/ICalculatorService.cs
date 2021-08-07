// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace ServiceContract
{
    [CoreWCF.ServiceContract]
    public interface ICalculatorService
    {
        [CoreWCF.OperationContract]
        [CoreWCF.FaultContract(typeof(MathFault))]
        int Divide(int a, int b);
    }  

    [DataContract]
    public class MathFault
    {
        [DataMember]
        public string Error { get; set; }
    }  
}
