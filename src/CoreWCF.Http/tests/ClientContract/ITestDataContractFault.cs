// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ClientContract
{
    #region Contract w/ FaultContract using DataContract (ITestDataContractFault)
    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class SomeFault
    {
        public SomeFault(int errID, string errMsg)
        {
            ID = errID;
            message = errMsg;
        }
        [DataMember]
        public int ID;
        [DataMember]
        public string message;
    }

    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class OuterFault
    {
        [DataMember]
        public SomeFault InnerFault { get; set; }
    }

    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class ComplexFault
    {
        [DataMember]
        public int ErrorInt { get; set; }

        [DataMember]
        public string ErrorString { get; set; }

        [DataMember]
        public SomeFault SomeFault { get; set; }

        [DataMember]
        public byte[] ErrorByteArray { get; set; }

        [DataMember]
        public int[] ErrorIntArray { get; set; }

        [DataMember]
        public string[] ErrorStringArray { get; set; }

        [DataMember]
        public SomeFault[] SomeFaultArray { get; set; }
    }

    [ServiceContract]
    public interface ITestDataContractFault
    {
        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        string TwoWay_Method(string s);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        void TwoWayVoid_Method(string s);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        Stream TwoWayStream_Method(Stream s);

        [OperationContract(AsyncPattern = true)]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        System.Threading.Tasks.Task<string> TwoWayAsync_Method(string s);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        FaultMsgContract MessageContract_Method(FaultMsgContract fmc);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        Message Untyped_Method(Message m);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        Message Untyped_MethodReturns(Message m);
    }

    [ServiceContract(Name = "ITestDataContractFault")]
    public interface ITestDataContractFaultTypedClient
    {
        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        string Untyped_Method(string s);

        [OperationContract]
        [FaultContract(typeof(SomeFault))]
        [FaultContract(typeof(OuterFault))]
        [FaultContract(typeof(ComplexFault))]
        string Untyped_MethodReturns(string s);
    }
    #endregion
}
