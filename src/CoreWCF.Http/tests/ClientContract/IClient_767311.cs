// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract(Name = "ISyncService")]
    public interface IClientAsync_767311
    {
        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginEchoString(string s, AsyncCallback callback, object state);

        string EndEchoString(IAsyncResult result);
    }
}
