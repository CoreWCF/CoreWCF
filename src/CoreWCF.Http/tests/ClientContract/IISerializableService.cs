using ServiceContract;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    [ServiceKnownType(typeof(mYStruct))]
    public interface IISerializableService
    {
        [OperationContract()]
        void Method_MyISerClass_out(MyISerClass obj1, out MyISerClass obj2);

        [OperationContract()]
        MyISerClass Method_MyISerClass_ref(ref MyISerClass obj1);

        [OperationContract()]
        MyISerClass Method_MyISerClass(MyISerClass obj1);

        [OperationContract()]
        void Method_MyISerStruct_out(MyISerStruct obj1, out MyISerStruct obj2);

        [OperationContract()]
        MyISerStruct Method_MyISerStruct_ref(ref MyISerStruct obj1);

        [OperationContract()]
        MyISerStruct Method_MyISerStruct(MyISerStruct obj1);

        [OperationContract()]
        void Method_MyISerClassFromClass_out(MyISerClassFromClass obj1, out MyISerClassFromClass obj2);

        [OperationContract()]
        MyISerClassFromClass Method_MyISerClassFromClass_ref(ref MyISerClassFromClass obj1);

        [OperationContract()]
        MyISerClassFromClass Method_MyISerClassFromClass(MyISerClassFromClass obj1);

        [OperationContract()]
        void Method_MyISerClassFromSerializable_out(MyISerClassFromSerializable obj1, out MyISerClassFromSerializable obj2);

        [OperationContract()]
        MyISerClassFromSerializable Method_MyISerClassFromSerializable_ref(ref MyISerClassFromSerializable obj1);

        [OperationContract()]
        MyISerClassFromSerializable Method_MyISerClassFromSerializable(MyISerClassFromSerializable obj1);

        [OperationContract()]
        void Method_BoxedStructHolder_out(BoxedStructHolder obj1, out BoxedStructHolder obj2);

        [OperationContract()]
        BoxedStructHolder Method_BoxedStructHolder_ref(ref BoxedStructHolder obj1);

        [OperationContract()]
        BoxedStructHolder Method_BoxedStructHolder(BoxedStructHolder obj1);
    }
}