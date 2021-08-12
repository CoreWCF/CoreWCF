// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface ITestFaultContractName1
    {
        [OperationContract]
        [FaultContract(typeof(string), Name = "foo")]
        string Method1(string s);
    }
}
