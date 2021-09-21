// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ServiceContract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.ECHOSERVICE_NAME)]
    public interface IEchoService
    {
        [OperationContract(Name = "EchoString", Action = Constants.OPERATION_BASE + "EchoString",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringResponse")]
        string EchoString(string echo);

        [OperationContract(Name = "EchoStream", Action = Constants.OPERATION_BASE + "EchoStream",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamResponse")]
        Stream EchoStream(Stream echo);

        [OperationContract(Name = "EchoStringAsync", Action = Constants.OPERATION_BASE + "EchoStringAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringAsyncResponse")]
        Task<string> EchoStringAsync(string echo);

        [OperationContract(Name = "EchoStreamAsync", Action = Constants.OPERATION_BASE + "EchoStreamAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamAsyncResponse")]
        Task<Stream> EchoStreamAsync(Stream echo);

        [OperationContract(Name = "EchoToFail", Action = Constants.OPERATION_BASE + "EchoToFail",
ReplyAction = Constants.OPERATION_BASE + "EchoToFailResponse")]
        string EchoToFail(string echo);

    }
}
