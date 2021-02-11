// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    internal static class AddressingStrings
    {
        // Main dictionary strings
        public const string Action = ServiceModelStringsVersion1.String5;
        public const string To = ServiceModelStringsVersion1.String6;
        public const string RelatesTo = ServiceModelStringsVersion1.String9;
        public const string MessageId = ServiceModelStringsVersion1.String13;
        public const string Address = ServiceModelStringsVersion1.String21;
        public const string ReplyTo = ServiceModelStringsVersion1.String22;
        public const string Empty = ServiceModelStringsVersion1.String81;
        public const string From = ServiceModelStringsVersion1.String82;
        public const string FaultTo = ServiceModelStringsVersion1.String83;
        public const string EndpointReference = ServiceModelStringsVersion1.String84;
        public const string PortType = ServiceModelStringsVersion1.String85;
        public const string ServiceName = ServiceModelStringsVersion1.String86;
        public const string PortName = ServiceModelStringsVersion1.String87;
        public const string ReferenceProperties = ServiceModelStringsVersion1.String88;
        public const string RelationshipType = ServiceModelStringsVersion1.String89;
        public const string Reply = ServiceModelStringsVersion1.String90;
        public const string Prefix = ServiceModelStringsVersion1.String91;
        public const string IdentityExtensionNamespace = ServiceModelStringsVersion1.String92;
        public const string Identity = ServiceModelStringsVersion1.String93;
        public const string Spn = ServiceModelStringsVersion1.String94;
        public const string Upn = ServiceModelStringsVersion1.String95;
        public const string Rsa = ServiceModelStringsVersion1.String96;
        public const string Dns = ServiceModelStringsVersion1.String97;
        public const string X509v3Certificate = ServiceModelStringsVersion1.String98;
        public const string ReferenceParameters = ServiceModelStringsVersion1.String100;
        public const string IsReferenceParameter = ServiceModelStringsVersion1.String101;
        // String constants
        public const string EndpointUnavailable = "EndpointUnavailable";
        public const string ActionNotSupported = "ActionNotSupported";
        public const string EndpointReferenceType = "EndpointReferenceType";
        public const string Request = "Request";
        public const string DestinationUnreachable = "DestinationUnreachable";
        public const string AnonymousUri = "http://schemas.microsoft.com/2005/12/ServiceModel/Addressing/Anonymous";
        public const string NoneUri = "http://schemas.microsoft.com/2005/12/ServiceModel/Addressing/None";
        public const string IndigoNamespace = "http://schemas.microsoft.com/serviceModel/2004/05/addressing";
        public const string ChannelTerminated = "ChannelTerminated";
    }

    internal static class Addressing10Strings
    {
        // Main dictionary strings
        public const string Namespace = ServiceModelStringsVersion1.String3;
        public const string Anonymous = ServiceModelStringsVersion1.String10;
        public const string FaultAction = ServiceModelStringsVersion1.String99;
        public const string ReplyRelationship = ServiceModelStringsVersion1.String102;
        public const string NoneAddress = ServiceModelStringsVersion1.String103;
        public const string Metadata = ServiceModelStringsVersion1.String104;
        // String constants
        public const string MessageAddressingHeaderRequired = "MessageAddressingHeaderRequired";
        public const string InvalidAddressingHeader = "InvalidAddressingHeader";
        public const string InvalidCardinality = "InvalidCardinality";
        public const string ActionMismatch = "ActionMismatch";
        public const string ProblemHeaderQName = "ProblemHeaderQName";
        public const string FaultDetail = "FaultDetail";
        public const string DefaultFaultAction = "http://www.w3.org/2005/08/addressing/soap/fault";
    }
}
