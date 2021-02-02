// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using CoreWCF;
using CoreWCF.Channels;

namespace Services
{
    [ServiceBehavior]
    public class FaultOnDiffContractsAndOpsService : ServiceContract.ITestFaultOpContract
    {
        #region TwoWay_Methods
        public string TwoWay_Method(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public void TwoWayVoid_Method(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return;
        }
        #endregion

        #region TwoWayStream Method
        public Stream TwoWayStream_Method(Stream s)
        {
            if (s.ReadByte() != -1)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }
            else
            {
                StreamReader sr = new StreamReader(s, Encoding.UTF8);
                sr.ReadToEnd();
            }

            return new MemoryStream();
        }
        #endregion

        #region TwoWayAsync Method
        private delegate string TwoWayMethod(string s);

        public async System.Threading.Tasks.Task<string> TwoWayAsync_MethodAsync(string s)
        {
            TwoWayMethod del = ProcessTwoWay;
            var workTask = System.Threading.Tasks.Task.Run(() => del.Invoke(s));
            return await workTask;
        }

        // Worker
        public static string ProcessTwoWay(string s)
        {
            // This is where the incoming message processing is handled.
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return "Async call was valid";
        }
        #endregion

        #region MessageContract Methods
        public ServiceContract.FaultMsgContract MessageContract_Method(ServiceContract.FaultMsgContract fmc)
        {
            if (fmc.Name.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return fmc;
        }

        public static string MessageContractParams_Method(int id, string name, DateTime dateTime)
        {
            if (name.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return $"{id} {name} {dateTime}";
        }
        #endregion

        #region Untyped Method
        public CoreWCF.Channels.Message Untyped_Method(CoreWCF.Channels.Message msgIn)
        {
            CoreWCF.Channels.MessageVersion mv = OperationContext.Current.IncomingMessageHeaders.MessageVersion;
            string faultToThrow = "Test fault thrown from a service";
            if (msgIn != null)
            {
                throw new FaultException<string>(faultToThrow);
            }

            return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspecified",
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
        }

        public CoreWCF.Channels.Message Untyped_MethodReturns(CoreWCF.Channels.Message msgIn)
        {
            CoreWCF.Channels.MessageVersion mv = OperationContext.Current.IncomingMessageHeaders.MessageVersion;
            string faultToThrow = "Test fault thrown from a service";
            if (msgIn != null)
            {
                return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), faultToThrow,
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
            }

            return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspeficied",
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
        }
        #endregion
    }

    [ServiceBehavior]
    public class DatacontractFaultService : ServiceContract.ITestDataContractFault
    {
        private const string VarSomeFault = "somefault";
        private const string VarOuterFault = "outerfault";
        private const string VarComplexFault = "complexfault";

        private void ThrowTestFault(string faultType)
        {
            //this.tenv.ExecutionEngine.Results.Write("Throwing Fault'[{0}]", faultToThrow);
            switch (faultType.ToLower())
            {
                case VarSomeFault:
                    throw new FaultException<ServiceContract.SomeFault>(new ServiceContract.SomeFault(123456789, "SomeFault"));
                case VarOuterFault:
                    ServiceContract.SomeFault sf = new ServiceContract.SomeFault(123456789, "SomeFault as innerfault");
                    ServiceContract.OuterFault of = new ServiceContract.OuterFault
                    {
                        InnerFault = sf
                    };
                    throw new FaultException<ServiceContract.OuterFault>(of);
                case VarComplexFault:
                    ServiceContract.ComplexFault cf = GetComplexFault();
                    throw new FaultException<ServiceContract.ComplexFault>(cf);
                default:
                    throw new ApplicationException("Unknown value of FaultString specified: " + faultType);
            }
        }

        #region TwoWay_Methods
        public string TwoWay_Method(string s)
        {
            if (s.Length != 0)
            {
                ThrowTestFault(s);
            }

            return s;
        }

        public void TwoWayVoid_Method(string s)
        {
            if (s.Length != 0)
            {
                ThrowTestFault(s);
            }

            return;
        }
        #endregion

        #region TwoWayStream Method
        public Stream TwoWayStream_Method(Stream s)
        {
            StreamReader sr = new StreamReader(s, Encoding.UTF8);
            ThrowTestFault(sr.ReadToEnd());
            Stream outStream = new MemoryStream();
            return outStream;
        }
        #endregion

        #region TwoWayAsync Method

        public async System.Threading.Tasks.Task<string> TwoWayAsync_Method(string s)
        {
            var workTask = System.Threading.Tasks.Task.Run(() => ProcessTwoWayAsync(s));
            return await workTask;
        }

        // Worker
        public string ProcessTwoWayAsync(string s)
        {
            // This is where the incoming message processing is handled.
            if (s.Length != 0)
            {
                ThrowTestFault(s);
            }

            string response = "Async call was valid";
            return response;
        }
        #endregion

        #region MessageContract Methods
        public ServiceContract.FaultMsgContract MessageContract_Method(ServiceContract.FaultMsgContract fmc)
        {
            if (fmc.Name.Length != 0)
            {
                ThrowTestFault(fmc.Name);
            }

            return fmc;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public string MessageContractParams_Method(int id, string Name, DateTime dateTime)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            if (Name.Length != 0)
            {
                ThrowTestFault(Name);
            }

            return Name;
        }
        #endregion

        #region Untyped Method
        public CoreWCF.Channels.Message Untyped_Method(CoreWCF.Channels.Message msgIn)
        {
            if (null != msgIn)
            {
                ThrowTestFault(GetTestStrFromMsg(msgIn));
            }

            return Message.CreateMessage(MessageVersion.Soap11, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspecified",
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "http://www.w3.org/2005/08/addressing/fault");
        }

        public CoreWCF.Channels.Message Untyped_MethodReturns(CoreWCF.Channels.Message msgIn)
        {
            MessageVersion mv = MessageVersion.Soap11;

            if (null != msgIn)
            {
                string faultString = GetTestStrFromMsg(msgIn);
                switch (faultString)
                {
                    case VarSomeFault:
                        return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), (object)new ServiceContract.SomeFault(123456789, "SomeFault"),
        new System.Runtime.Serialization.DataContractSerializer(typeof(ServiceContract.SomeFault)), "", ""), "http://tempuri.org/ITestDataContractFault/Untyped_MethodReturnsSomeFaultFault");
                    case VarOuterFault:
                        ServiceContract.SomeFault sf = new ServiceContract.SomeFault(123456789, "SomeFault as innerfault");
                        ServiceContract.OuterFault of = new ServiceContract.OuterFault
                        {
                            InnerFault = sf
                        };
                        return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), (object)of,
                new System.Runtime.Serialization.DataContractSerializer(typeof(ServiceContract.OuterFault)), "", ""), "http://tempuri.org/ITestDataContractFault/Untyped_MethodReturnsOuterFaultFault");
                    case VarComplexFault:
                        ServiceContract.ComplexFault cf = GetComplexFault();
                        return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), (object)cf,
        new System.Runtime.Serialization.DataContractSerializer(typeof(ServiceContract.ComplexFault)), "", ""), "http://tempuri.org/ITestDataContractFault/Untyped_MethodReturnsComplexFaultFault");

                    default:
                        throw new ApplicationException("Unknown value of FaultString specified: " + faultString);
                }
            }

            return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspecified",
        new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "http://www.w3.org/2005/08/addressing/fault");
        }
        #endregion

        private string GetTestStrFromMsg(Message msgIn)
        {
            string msgBody = msgIn.GetReaderAtBodyContents().ReadInnerXml();
            if (msgBody.Contains(VarSomeFault))
            {
                return VarSomeFault;
            }

            if (msgBody.Contains(VarOuterFault))
            {
                return VarOuterFault;
            }

            if (msgBody.Contains(VarComplexFault))
            {
                return VarComplexFault;
            }

            return "";
        }

        private ServiceContract.ComplexFault GetComplexFault()
        {
            int errorInt = 50;
            string errorString = "This is a test error string for fault tests.";

            ServiceContract.SomeFault errorSomeFault = new ServiceContract.SomeFault(123456789, "SomeFault in complexfault");

            int[] errorIntArray = new int[]
                    {
                        int.MaxValue,
                        int.MinValue,
                        0,
                        1,
                        -1,
                        50,
                        -50
                    };

            string[] errorStringArray = new string[]
                    {
                        string.Empty,
                        null,
                        "String Value"
                    };

            byte[] errorByteArray = new byte[128];
            for (int i = 0; i < errorByteArray.Length; i++)
            {
                errorByteArray[i] = (byte)i;
            }

            ServiceContract.SomeFault[] errorSomeFaultArray = new ServiceContract.SomeFault[]
                    {
                        errorSomeFault,
                        null,
                        new ServiceContract.SomeFault(234, "Second somefault in complexfault")
                    };

            ServiceContract.ComplexFault cf = new ServiceContract.ComplexFault
            {
                ErrorInt = errorInt,
                ErrorString = errorString,
                ErrorByteArray = errorByteArray,
                SomeFault = errorSomeFault,
                ErrorIntArray = errorIntArray,
                ErrorStringArray = errorStringArray,
                SomeFaultArray = errorSomeFaultArray
            };

            return cf;
        }
    }
}
