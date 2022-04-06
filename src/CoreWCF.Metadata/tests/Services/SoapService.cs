// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ServiceContract;

namespace Services
{
    internal class SoapService : IWcfSoapService
    {
        public string CombineStringXmlSerializerFormatSoap(string message1, string message2) => message1 + message2;
        public SoapComplexType EchoComositeTypeXmlSerializerFormatSoap(SoapComplexType c) => new SoapComplexType { BoolValue = c.BoolValue, StringValue = c.StringValue };
        public PingEncodedResponse Ping(PingEncodedRequest request) => new PingEncodedResponse { Return = request.Pinginfo?.Length ?? 0 };
        public string ProcessCustomerData(CustomerObject CustomerData) => CustomerData.Name;
    }
}
