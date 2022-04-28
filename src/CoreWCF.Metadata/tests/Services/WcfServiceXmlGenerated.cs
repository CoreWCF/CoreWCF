// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ServiceContract;

namespace Services
{
    internal class WcfServiceXmlGenerated : IWcfServiceXmlGenerated
    {
        public string EchoXmlSerializerFormat(string message) => message;

        public string EchoXmlSerializerFormatSupportFaults(string message, bool pleaseThrowException) => !pleaseThrowException ? message : throw new Exception(message);

        public string EchoXmlSerializerFormatUsingRpc(string message) => message;

        public XmlVeryComplexType EchoXmlVeryComplexType(XmlVeryComplexType complex) => complex;

        public XmlCompositeType GetDataUsingXmlSerializer(XmlCompositeType composite) => composite;
    }
}
