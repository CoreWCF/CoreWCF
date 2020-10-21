//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
using System;

namespace CoreWCF.Http.Tests
{
    public static class Constants
    {
        public const string binding = "binding";
        public const string DefaultHttpsCertificateName = "WebSocketCertificate";
        public const string NotSet = "NotSet";
        public const int DefaultMaxReceivedMessageSize = 64 * 1024 * 1024;
        public const string WebSocketMessageProperty = "WebSocketMessageProperty";
        public const string StopPublishing = "StopPublishing";
        public const string LastMessage = "LastMessage";

        public const string RequestReplyWebSocketServiceClient = "RequestReplyServiceClient";
        public const string DuplexWebSocketServiceClient = "DuplexWebSocketServiceClient";
        public const string IRequestReplyService = "IRequestReplyService";
        public const string IDuplexService = "IDuplexService";

        public const string DownloadData = "DownloadData";
        public const string UploadData = "UploadData";
        public const string DownloadStream = "DownloadStream";
        public const string UploadStream = "UploadStream";
        public const string GetLog = "GetLog";
        public const string CallbackImplementation = "CallbackImplementation";
        public const string CallbackImplementationCodeFile = "CallbackImplementation.cs";
        public const string WebSocketsProxyUri = "http://nclmsftproxy.redmond.corp.microsoft.com:8080";
        public const string RemoteEndpointMessagePropertyFailure = "RemoteEndpointMessageProperty did not contain the address of this machine.";
    }

    public enum WebSocketBindingType
    {
        BasicHttp,
        NetHttp,
        NetHttps,
        CustomDeriveFromBasic,
        CustomDeriveFromWSHttp,
        CustomDeriveFromNetNamedPipe,
        CustomDeriveFromNetTcp,
        NetHttpByteStreamMessageEncoder
    }

    public enum EncoderForBinding
    {
        Text,
        Binary,
        Mtom,
        Custom
    }

    public enum WebSocketServiceContract
    {
        IRequestReplyService,
        IDuplexService,
        IPublish
    }

    public enum WebSocketTimeoutCategory
    {
        None,
        ClientSendTimeout,
        ClientReceiveTimeout,
        ClientOpenTimeout,
        ServerSendTimeout,
        ServerReceiveTimeout,
        OperationTimeout,
        ServerOpenTimeout
    }
}