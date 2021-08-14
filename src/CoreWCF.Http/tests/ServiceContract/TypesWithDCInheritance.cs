using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [DataContract]
    public class TypeWithDCInheritingFromSer : TypeWithSerInheritingFromDC
    {
        new public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;

        [DataMember]
        new public string TheData = "hello1";

        [OnSerializing]
        void OnSerializingMethod(StreamingContext cxt)
        {
            ((TypeWithDCInheritingFromSer)this).myCallBacksCalled |= CallBacksCalled.OnSerializing;
        }

        [OnSerialized]
        void OnSerializedMethod(StreamingContext cxt)
        {
            ((TypeWithDCInheritingFromSer)this).myCallBacksCalled |= CallBacksCalled.OnSerialized;
        }

        [OnDeserializing]
        void OnDeserializingMethod(StreamingContext cxt)
        {
            ((TypeWithDCInheritingFromSer)this).myCallBacksCalled |= CallBacksCalled.OnDeserializing;
        }

        [OnDeserialized]
        void OnDeserializedMethod(StreamingContext cxt)
        {
            ((TypeWithDCInheritingFromSer)this).myCallBacksCalled |= CallBacksCalled.OnDeserialized;
        }

        public static bool Validate(TypeWithDCInheritingFromSer obj1, TypeWithDCInheritingFromSer obj2)
        {
            bool value = Helpers.DCDataComparer.Compare((object)obj1, (object)obj2);
            if (value == false)
            {
                Trace.WriteLine("DataContractCompares do not match");
                return false;
            }
            // assume one has been serialized and the other deserialized
            if ((obj1.myCallBacksCalled & obj2.myCallBacksCalled) == CallBacksCalled.None)
            {
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.All)
                {
                    return true;
                }
            }

            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return TypeWithSerInheritingFromDC.Validate((TypeWithSerInheritingFromDC)obj1, (TypeWithSerInheritingFromDC)obj2);
        }
    }

    [Serializable]
    public class TypeWithSerInheritingFromDC : BaseDC
    {
        new public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
        new public string TheData = "hello2";

        [OnSerializing]
        void OnSerializingMethod(StreamingContext cxt)
        {
            ((TypeWithSerInheritingFromDC)this).myCallBacksCalled |= CallBacksCalled.OnSerializing;
        }

        [OnSerialized]
        void OnSerializedMethod(StreamingContext cxt)
        {
            ((TypeWithSerInheritingFromDC)this).myCallBacksCalled |= CallBacksCalled.OnSerialized;
        }

        [OnDeserializing]
        void OnDeserializingMethod(StreamingContext cxt)
        {
            ((TypeWithSerInheritingFromDC)this).myCallBacksCalled |= CallBacksCalled.OnDeserializing;
        }

        [OnDeserialized]
        void OnDeserializedMethod(StreamingContext cxt)
        {
            ((TypeWithSerInheritingFromDC)this).myCallBacksCalled |= CallBacksCalled.OnDeserialized;
        }

        public static bool Validate(TypeWithSerInheritingFromDC obj1, TypeWithSerInheritingFromDC obj2)
        {
            bool value = Helpers.DCDataComparer.Compare((object)obj1, (object)obj2);
            if (value == false)
            {
                Trace.WriteLine("DataContractCompares do not match");
                return false;
            }
            // assume one has been serialized and the other deserialized
            if ((obj1.myCallBacksCalled & obj2.myCallBacksCalled) == CallBacksCalled.None)
            {
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.All)
                {
                    return true;
                }
            }

            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return BaseDC.Validate((BaseDC)obj1, (BaseDC)obj2);
        }
    }

    [DataContract]
    public class BaseDC
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;

        [DataMember]
        public string TheData = "hello3";

        [OnSerializing]
        void OnSerializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerializing;
        }

        [OnSerialized]
        void OnSerializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerialized;
        }

        [OnDeserializing]
        void OnDeserializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserializing;
        }

        [OnDeserialized]
        void OnDeserializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserialized;
        }

        public static bool Validate(BaseDC obj1, BaseDC obj2)
        {
            bool value = Helpers.DCDataComparer.Compare((object)obj1, (object)obj2);
            if (value == false)
            {
                Trace.WriteLine("DataContractCompares do not match");
                return false;
            }
            // assume one has been serialized and the other deserialized
            if ((obj1.myCallBacksCalled & obj2.myCallBacksCalled) == CallBacksCalled.None)
            {
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.All)
                {
                    return true;
                }
            }

            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return false;
        }
    }
}