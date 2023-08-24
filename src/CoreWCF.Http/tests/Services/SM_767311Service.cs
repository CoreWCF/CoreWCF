// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Services
{
    public class SM_767311Service : ServiceContract.ISyncService
    {
        public string EchoString(string s)
        {
            Thread.Sleep(5000);
            string response = "Async call was valid";
            return response;
        }
    }
}
