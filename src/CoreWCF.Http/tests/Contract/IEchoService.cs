// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;

namespace Contract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [CoreWCF.ServiceContract(Namespace = Constants.NS, Name = Constants.ECHOSERVICE_NAME)]
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS, Name = Constants.ECHOSERVICE_NAME)]
    public interface IEchoService
    {
        [CoreWCF.OperationContract(Name = "EchoString", Action = Constants.OPERATION_BASE + "EchoString",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringResponse")]
        [System.ServiceModel.OperationContract(Name = "EchoString", Action = Constants.OPERATION_BASE + "EchoString",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringResponse")]
        string EchoString(string echo);

        [CoreWCF.OperationContract(Name = "EchoStream", Action = Constants.OPERATION_BASE + "EchoStream",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamResponse")]
        [System.ServiceModel.OperationContract(Name = "EchoStream", Action = Constants.OPERATION_BASE + "EchoStream",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamResponse")]
        Stream EchoStream(Stream echo);

        [CoreWCF.OperationContract(Name = "EchoStringAsync", Action = Constants.OPERATION_BASE + "EchoStringAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringAsyncResponse")]
        [System.ServiceModel.OperationContract(Name = "EchoStringAsync", Action = Constants.OPERATION_BASE + "EchoStringAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringAsyncResponse")]
        Task<string> EchoStringAsync(string echo);

        [CoreWCF.OperationContract(Name = "EchoStreamAsync", Action = Constants.OPERATION_BASE + "EchoStreamAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamAsyncResponse")]
        [System.ServiceModel.OperationContract(Name = "EchoStreamAsync", Action = Constants.OPERATION_BASE + "EchoStreamAsync",
            ReplyAction = Constants.OPERATION_BASE + "EchoStreamAsyncResponse")]
        Task<Stream> EchoStreamAsync(Stream echo);
    }
}