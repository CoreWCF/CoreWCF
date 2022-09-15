using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IDataContractInheritanceService
    {
        [OperationContract()]
        void Method_TypeWithDCInheritingFromSer_out(TypeWithDCInheritingFromSer obj1, out TypeWithDCInheritingFromSer obj2);

        [OperationContract()]
        TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer_ref(ref TypeWithDCInheritingFromSer obj1);

        [OperationContract()]
        TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer(TypeWithDCInheritingFromSer obj1);

        [OperationContract()]
        void Method_TypeWithSerInheritingFromDC_out(TypeWithSerInheritingFromDC obj1, out TypeWithSerInheritingFromDC obj2);

        [OperationContract()]
        TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC_ref(ref TypeWithSerInheritingFromDC obj1);

        [OperationContract()]
        TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC(TypeWithSerInheritingFromDC obj1);

        [OperationContract()]
        void Method_BaseDC_out(BaseDC obj1, out BaseDC obj2);

        [OperationContract()]
        BaseDC Method_BaseDC_ref(ref BaseDC obj1);

        [OperationContract()]
        BaseDC Method_BaseDC(BaseDC obj1);
    }
}