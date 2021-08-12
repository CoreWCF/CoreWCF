// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace Services
{
    [ServiceContract]
    public class ServiceWithSC
    {
        [OperationContract]
        public string BaseStringMethod(string str)
        {
            return str;
        }
    }
}
