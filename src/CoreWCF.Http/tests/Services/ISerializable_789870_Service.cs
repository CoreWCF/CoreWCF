using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class ISerializable_789870_Service : IISerializableService
    {
        public void Method_MyISerClass_out(MyISerClass obj1, out MyISerClass obj2)
        {
            obj2 = obj1;
            return;
        }

        public MyISerClass Method_MyISerClass_ref(ref MyISerClass obj1)
        {
            return obj1;
        }

        public MyISerClass Method_MyISerClass(MyISerClass obj1)
        {
            return obj1;
        }

        public void Method_MyISerStruct_out(MyISerStruct obj1, out MyISerStruct obj2)
        {
            obj2 = obj1;
            return;
        }

        public MyISerStruct Method_MyISerStruct_ref(ref MyISerStruct obj1)
        {
            return obj1;
        }

        public MyISerStruct Method_MyISerStruct(MyISerStruct obj1)
        {
            return obj1;
        }

        public void Method_MyISerClassFromClass_out(MyISerClassFromClass obj1, out MyISerClassFromClass obj2)
        {
            obj2 = obj1;
            return;
        }

        public MyISerClassFromClass Method_MyISerClassFromClass_ref(ref MyISerClassFromClass obj1)
        {
            return obj1;
        }

        public MyISerClassFromClass Method_MyISerClassFromClass(MyISerClassFromClass obj1)
        {
            return obj1;
        }

        public void Method_BoxedStructHolder_out(BoxedStructHolder obj1, out BoxedStructHolder obj2)
        {
            obj2 = obj1;
            return;
        }

        public BoxedStructHolder Method_BoxedStructHolder_ref(ref BoxedStructHolder obj1)
        {
            return obj1;
        }

        public BoxedStructHolder Method_BoxedStructHolder(BoxedStructHolder obj1)
        {
            return obj1;
        }

        public void Method_MyISerClassFromSerializable_out(MyISerClassFromSerializable obj1, out MyISerClassFromSerializable obj2)
        {
            obj2 = obj1;
            return;
        }

        public MyISerClassFromSerializable Method_MyISerClassFromSerializable_ref(ref MyISerClassFromSerializable obj1)
        {
            return obj1;
        }

        public MyISerClassFromSerializable Method_MyISerClassFromSerializable(MyISerClassFromSerializable obj1)
        {
            return obj1;
        }
    }
}