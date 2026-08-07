// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Extensions.Configuration.Tests.ClientContract
{
    /// <summary>
    /// The client half of the echo contract. It is a separate declaration attributed with the WCF client's
    /// <see cref="System.ServiceModel.ServiceContractAttribute"/> rather than CoreWCF's, and matches the service
    /// side by SOAP name rather than by CLR type - which is the contract sharing question left open in
    /// microsoft/aspire#3994.
    /// </summary>
    [System.ServiceModel.ServiceContract(Name = "IEchoService")]
    public interface IEchoService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string value);
    }
}
