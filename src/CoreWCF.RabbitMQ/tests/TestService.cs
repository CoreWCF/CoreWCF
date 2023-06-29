// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using CoreWCF;

namespace Contracts
{
    [System.ServiceModel.ServiceContract]
    [ServiceContract]
    public interface ITestContract
    {
        [System.ServiceModel.OperationContract(IsOneWay = true)]
        [OperationContract(IsOneWay = true)]
        void Create(string name);
    }   

    public class TestService : ITestContract
    {  
        public TestService()
        {
            ManualResetEvent = new ManualResetEventSlim(false);
        }

        public void Create(string name)
        {
            ManualResetEvent.Set();
        }
        public ManualResetEventSlim ManualResetEvent { get; }
    }
}
