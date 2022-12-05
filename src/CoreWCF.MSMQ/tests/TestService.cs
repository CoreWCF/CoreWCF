// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using CoreWCF;

namespace Contracts
{
    [ServiceContract]
    public interface ITestContract
    {
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
            if (string.IsNullOrEmpty(name))
                throw new FaultException();

            ManualResetEvent.Set();
        }

        public ManualResetEventSlim ManualResetEvent { get; }
    }
}
