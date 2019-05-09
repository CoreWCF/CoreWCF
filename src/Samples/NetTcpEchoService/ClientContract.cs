using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

namespace NetTcpEchoServiceSample.Client
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = "http://tempuri.org/", Name = "IEchoService")]
    public interface IEchoService
    {
        [OperationContract]
        string EchoString(string echo);
    }
}
