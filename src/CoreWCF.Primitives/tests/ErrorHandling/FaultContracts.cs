﻿using CoreWCF;
using DispatcherClient;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.Serialization;
using Xunit;

namespace ErrorHandling
{
    public class FaultContracts
    {
        [Fact]
        public static void ServiceSendFaultMessage()
        {
            var fault = new TestFault { Message = Guid.NewGuid().ToString() };
            var reason = new FaultReason(Guid.NewGuid().ToString());
            var code = new FaultCode(nameof(ServiceSendFaultMessage));
            var factory = DispatcherHelper.CreateChannelFactory<FaultingService, IFaultingService>(
                (services) =>
                {
                    services.AddSingleton(new FaultingService(fault, reason, code));
                });
            factory.Open();
            var channel = factory.CreateChannel();
            Exception exceptionThrown = null;
            try
            {
                channel.FaultingOperation();
            }
            catch (Exception e)
            {   
                exceptionThrown = e;
            }

            Assert.NotNull(exceptionThrown);
            Assert.IsType<System.ServiceModel.FaultException<TestFault>>(exceptionThrown);
            var faultException = (System.ServiceModel.FaultException<TestFault>)exceptionThrown;
            Assert.Equal(fault.Message, faultException.Detail.Message);
            Assert.Equal(reason.ToString(), faultException.Reason.ToString());
            Assert.Equal(code.Name, faultException.Code.Name);
            // Empty string FaultCode namespace becomes default soap envelope ns
            Assert.Equal("http://www.w3.org/2003/05/soap-envelope", faultException.Code.Namespace);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public class FaultingService : IFaultingService
    {
        private TestFault _fault;
        private FaultReason _reason;
        private FaultCode _code;

        public FaultingService(TestFault fault, FaultReason reason, FaultCode code)
        {
            _fault = fault;
            _reason = reason;
            _code = code;
        }
        public void FaultingOperation()
        {
            throw new FaultException<TestFault>(_fault, _reason, _code);
        }
    }

    [System.ServiceModel.ServiceContract]
    public interface IFaultingService
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.FaultContract(typeof(TestFault))]
        [FaultContract(typeof(TestFault))]
        void FaultingOperation();
    }

    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class TestFault
    {
        [DataMember]
        public string Message { get; set; }
    }
}
