// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;

namespace Services
{
    [ServiceContract]
    public class ServiceWithSCExtendingFromServiceWithSC : ServiceWithSC
    {
        [OperationContract]
        private string DerivedStringMethod(string str)
        {
            throw new ApplicationException("Failed:Not expected to be invoked!!");
        }
    }
}
