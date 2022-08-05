// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.MSMQ.Tests.Fakes;

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
        private readonly Interceptor _interceptor;

        public TestService(Interceptor interceptor)
        {
            _interceptor = interceptor;
        }

        public void Create(string name)
        {
            _interceptor.SetName(name);
        }
    }
}
