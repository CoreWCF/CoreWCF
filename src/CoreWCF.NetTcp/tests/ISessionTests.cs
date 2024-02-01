// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace CoreWCF.NetTcp.Tests
{
    [DataContract]
    public class SessionTestsCompositeType
    {
        [DataMember]
        public int MethodAValue { get; set; }
        [DataMember]
        public int MethodBValue { get; set; }
    }

    internal class ISessionTestClient
    {
        [System.ServiceModel.ServiceContract(SessionMode = System.ServiceModel.SessionMode.Required)]
        public interface ISessionTest
        {
            [System.ServiceModel.OperationContract()]
            int MethodAInitiating(int a);

            [System.ServiceModel.OperationContract()]
            int MethodBNonInitiating(int b);

            [System.ServiceModel.OperationContract()]
            SessionTestsCompositeType MethodCTerminating();
        }
    }

    internal class ISessionTestService
    {
        [ServiceContract(SessionMode = SessionMode.Required)]
        public interface ISessionTest
        {
            [OperationContract(IsInitiating = true, IsTerminating = false)]
            int MethodAInitiating(int a);

            [OperationContract(IsInitiating = false, IsTerminating = false)]
            int MethodBNonInitiating(int b);

            [OperationContract(IsInitiating = false, IsTerminating = true)]
            SessionTestsCompositeType MethodCTerminating();
        }

        public class SessionTestService : ISessionTest
        {
            public int MethodAInitiating(int a) { return a; }
            public int MethodBNonInitiating(int b) { return b; }
            public SessionTestsCompositeType MethodCTerminating() { return new SessionTestsCompositeType(); }
        }
    }

}
