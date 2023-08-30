// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IDataContractNamespaceService
    {
        [OperationContract()]
        void Method_TypeWithRelativeNamespace_out(TypeWithRelativeNamespace obj1, out TypeWithRelativeNamespace obj2);

        [OperationContract()]
        TypeWithRelativeNamespace Method_TypeWithRelativeNamespace_ref(ref TypeWithRelativeNamespace obj1);

        [OperationContract()]
        TypeWithRelativeNamespace Method_TypeWithRelativeNamespace(TypeWithRelativeNamespace obj1);

        [OperationContract()]
        void Method_TypeWithNumberInNamespace_out(TypeWithNumberInNamespace obj1, out TypeWithNumberInNamespace obj2);

        [OperationContract()]
        TypeWithNumberInNamespace Method_TypeWithNumberInNamespace_ref(ref TypeWithNumberInNamespace obj1);

        [OperationContract()]
        TypeWithNumberInNamespace Method_TypeWithNumberInNamespace(TypeWithNumberInNamespace obj1);

        [OperationContract()]
        void Method_TypeWithEmptyNamespace_out(TypeWithEmptyNamespace obj1, out TypeWithEmptyNamespace obj2);

        [OperationContract()]
        TypeWithEmptyNamespace Method_TypeWithEmptyNamespace_ref(ref TypeWithEmptyNamespace obj1);

        [OperationContract()]
        TypeWithEmptyNamespace Method_TypeWithEmptyNamespace(TypeWithEmptyNamespace obj1);

        [OperationContract()]
        void Method_TypeWithLongNamespace_out(TypeWithLongNamespace obj1, out TypeWithLongNamespace obj2);

        [OperationContract()]
        TypeWithLongNamespace Method_TypeWithLongNamespace_ref(ref TypeWithLongNamespace obj1);

        [OperationContract()]
        TypeWithLongNamespace Method_TypeWithLongNamespace(TypeWithLongNamespace obj1);

        [OperationContract()]
        void Method_TypeWithDefaultNamespace_out(TypeWithDefaultNamespace obj1, out TypeWithDefaultNamespace obj2);

        [OperationContract()]
        TypeWithDefaultNamespace Method_TypeWithDefaultNamespace_ref(ref TypeWithDefaultNamespace obj1);

        [OperationContract()]
        TypeWithDefaultNamespace Method_TypeWithDefaultNamespace(TypeWithDefaultNamespace obj1);

        [OperationContract()]
        void Method_TypeWithKeywordsInNamespace_out(TypeWithKeywordsInNamespace obj1, out TypeWithKeywordsInNamespace obj2);

        [OperationContract()]
        TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace_ref(ref TypeWithKeywordsInNamespace obj1);

        [OperationContract()]
        TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace(TypeWithKeywordsInNamespace obj1);

        [OperationContract()]
        void Method_TypeWithUnicodeInNamespace_out(TypeWithUnicodeInNamespace obj1, out TypeWithUnicodeInNamespace obj2);

        [OperationContract()]
        TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace_ref(ref TypeWithUnicodeInNamespace obj1);

        [OperationContract()]
        TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace(TypeWithUnicodeInNamespace obj1);
    }
}