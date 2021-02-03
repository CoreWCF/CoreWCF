// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceContract]
    public class ServiceWithSCDerivingFromNonSCExtendingSC : NonSCExtendingSC_1138907, SCInterface_1138907
    {
        public string SCStringMethod(string str)
        {
            throw new ApplicationException("Failed:Not expected to be invoked!!");
        }

        public string NonSCStringMethod(string str)
        {
            throw new ApplicationException("Failed:Not expected to be invoked!!");
        }

        [OperationContract]
        public string ServiceWithSCStringMethod(string str)
        {
            throw new ApplicationException("Failed:Not expected to be invoked!!");
        }
    }
}
