using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [Flags]
    public enum CallBacksCalled : byte
    {
        None = 0,
        OnSerializing = 1,
        OnSerialized = 2,
        OnDeserializing = 4,
        OnDeserialized = 8,
        All = OnSerializing | OnSerialized | OnDeserializing | OnDeserialized
    }

    [DataContract]
    public class DataContractEvents_All
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
        [OnSerializing]
        private void OnSerializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerializing;
        }
        [OnSerialized]
        private void OnSerializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerialized;
        }
        [OnDeserializing]
        private void OnDeserializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserializing;
        }
        [OnDeserialized]
        private void OnDeserializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserialized;
        }

        public static bool Validate(DataContractEvents_All obj1, DataContractEvents_All obj2)
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

    [DataContract]
    public class DataContractEvents_OnSerializing
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
        [OnSerializing]
        private void OnSerializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerializing;
        }
       
        public static bool Validate(DataContractEvents_OnSerializing obj1, DataContractEvents_OnSerializing obj2)
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
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.OnSerializing)
                {
                    return true;
                }
            }
            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return false;
        }
    }
    [DataContract]
    public class DataContractEvents_OnSerialized
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
       
        [OnSerialized]
        private void OnSerializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnSerialized;
        }
       
        public static bool Validate(DataContractEvents_OnSerialized obj1, DataContractEvents_OnSerialized obj2)
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
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.OnSerialized)
                {
                    return true;
                }
            }
            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return false;
        }
    }

    [DataContract]
    public class DataContractEvents_OnDeserializing
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
        [OnDeserializing]
        private void OnDeserializingMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserializing;
        }
       
        public static bool Validate(DataContractEvents_OnDeserializing obj1, DataContractEvents_OnDeserializing obj2)
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
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.OnDeserializing)
                {
                    return true;
                }
            }
            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return false;
        }
    }

    [DataContract]
    public class DataContractEvents_OnDeserialized
    {
        public CallBacksCalled myCallBacksCalled = CallBacksCalled.None;
       
        [OnDeserialized]
        private void OnDeserializedMethod(StreamingContext cxt)
        {
            this.myCallBacksCalled |= CallBacksCalled.OnDeserialized;
        }

        public static bool Validate(DataContractEvents_OnDeserialized obj1, DataContractEvents_OnDeserialized obj2)
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
                if ((obj1.myCallBacksCalled | obj2.myCallBacksCalled) == CallBacksCalled.OnDeserialized)
                {
                    return true;
                }
            }
            Trace.WriteLine(String.Format("Something wrong obj1 = {0} obj2 = {1}", obj1.myCallBacksCalled, obj2.myCallBacksCalled));
            return false;
        }
    }
}