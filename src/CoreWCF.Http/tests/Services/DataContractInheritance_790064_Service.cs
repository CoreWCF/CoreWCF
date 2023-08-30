// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class DataContractInheritance_790064_Service : IDataContractInheritanceService
    {
        public void Method_TypeWithDCInheritingFromSer_out(TypeWithDCInheritingFromSer obj1, out TypeWithDCInheritingFromSer obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer_ref(ref TypeWithDCInheritingFromSer obj1)
        {
            return obj1;
        }

        public TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer(TypeWithDCInheritingFromSer obj1)
        {
            return obj1;
        }

        public void Method_TypeWithSerInheritingFromDC_out(TypeWithSerInheritingFromDC obj1, out TypeWithSerInheritingFromDC obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC_ref(ref TypeWithSerInheritingFromDC obj1)
        {
            return obj1;
        }

        public TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC(TypeWithSerInheritingFromDC obj1)
        {
            return obj1;
        }

        public void Method_BaseDC_out(BaseDC obj1, out BaseDC obj2)
        {
            obj2 = obj1;
            return;
        }

        public BaseDC Method_BaseDC_ref(ref BaseDC obj1)
        {
            return obj1;
        }

        public BaseDC Method_BaseDC(BaseDC obj1)
        {
            return obj1;
        }
    }
}