// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class DataContractNamespace_789916_Service : IDataContractNamespaceService
    {
        public void Method_TypeWithRelativeNamespace_out(TypeWithRelativeNamespace obj1, out TypeWithRelativeNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithRelativeNamespace Method_TypeWithRelativeNamespace_ref(ref TypeWithRelativeNamespace obj1)
        {
            return obj1;
        }

        public TypeWithRelativeNamespace Method_TypeWithRelativeNamespace(TypeWithRelativeNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithNumberInNamespace_out(TypeWithNumberInNamespace obj1, out TypeWithNumberInNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithNumberInNamespace Method_TypeWithNumberInNamespace_ref(ref TypeWithNumberInNamespace obj1)
        {
            return obj1;
        }

        public TypeWithNumberInNamespace Method_TypeWithNumberInNamespace(TypeWithNumberInNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithEmptyNamespace_out(TypeWithEmptyNamespace obj1, out TypeWithEmptyNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithEmptyNamespace Method_TypeWithEmptyNamespace_ref(ref TypeWithEmptyNamespace obj1)
        {
            return obj1;
        }

        public TypeWithEmptyNamespace Method_TypeWithEmptyNamespace(TypeWithEmptyNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithLongNamespace_out(TypeWithLongNamespace obj1, out TypeWithLongNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithLongNamespace Method_TypeWithLongNamespace_ref(ref TypeWithLongNamespace obj1)
        {
            return obj1;
        }

        public TypeWithLongNamespace Method_TypeWithLongNamespace(TypeWithLongNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithDefaultNamespace_out(TypeWithDefaultNamespace obj1, out TypeWithDefaultNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithDefaultNamespace Method_TypeWithDefaultNamespace_ref(ref TypeWithDefaultNamespace obj1)
        {
            return obj1;
        }

        public TypeWithDefaultNamespace Method_TypeWithDefaultNamespace(TypeWithDefaultNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithKeywordsInNamespace_out(TypeWithKeywordsInNamespace obj1, out TypeWithKeywordsInNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace_ref(ref TypeWithKeywordsInNamespace obj1)
        {
            return obj1;
        }

        public TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace(TypeWithKeywordsInNamespace obj1)
        {
            return obj1;
        }

        public void Method_TypeWithUnicodeInNamespace_out(TypeWithUnicodeInNamespace obj1, out TypeWithUnicodeInNamespace obj2)
        {
            obj2 = obj1;
            return;
        }

        public TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace_ref(ref TypeWithUnicodeInNamespace obj1)
        {
            return obj1;
        }

        public TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace(TypeWithUnicodeInNamespace obj1)
        {
            return obj1;
        }
    }
}