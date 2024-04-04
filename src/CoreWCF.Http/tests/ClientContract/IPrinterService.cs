// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    internal static class IPrinterServiceConstants
    {
        public const string NS = "http://tempuri.org/";
        public const string SERVICENAME = nameof(IPrinterService);
        public const string OPERATION_BASE = NS + SERVICENAME + "/";
    }

    [ServiceContract(Namespace = IPrinterServiceConstants.NS, Name = IPrinterServiceConstants.SERVICENAME)]
    public interface IPrinterService
    {
        [OperationContract(Name = "PrintAsync", Action = Constants.OPERATION_BASE + "PrintAsync", ReplyAction = Constants.OPERATION_BASE + "PrintAsyncResponse")]
        Task<string> PrintAsync();
    }
}
