// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface IServiceWithCoreWCFMessageParameter
    {
        [OperationContract]
        [return: MessageParameter(Name = "Output")]
        string Identity([MessageParameter(Name = "Input")] string msg);
    }

    [ServiceContract]
    public interface IServiceWithSSMMessageParameter
    {
        [OperationContract]
        [return: MessageParameter(Name = "Output")]
        string Identity([MessageParameter(Name = "Input")] string msg);
    }
}
