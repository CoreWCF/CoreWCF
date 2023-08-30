// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class Versioning_789896_Service_Old : IVersioningServiceOld
    {
        public void Method_OldContractA_out(OldContractA obj1, out OldContractA obj2)
        {
            obj2 = obj1;
            return;
        }

        public OldContractA Method_OldContractA_ref(ref OldContractA obj1)
        {
            return obj1;
        }

        public OldContractA Method_OldContractA(OldContractA obj1)
        {
            return obj1;
        }
    }

    [ServiceBehavior]
    public class Versioning_789896_Service_New : IVersioningServiceNew
    {
        public void Method_NewContractA_out(NewContractA obj1, out NewContractA obj2)
        {
            obj2 = obj1;
            return;
        }

        public NewContractA Method_NewContractA_ref(ref NewContractA obj1)
        {
            return obj1;
        }

        public NewContractA Method_NewContractA(NewContractA obj1)
        {
            return obj1;
        }
    }
}