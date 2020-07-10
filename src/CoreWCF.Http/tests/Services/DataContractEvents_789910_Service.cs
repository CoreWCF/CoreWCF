using CoreWCF;
using ServiceContract;

namespace Services
{

    [ServiceBehavior]
    public class DataContractEvents_789910_Service : IDataContractEventsService
    {
        public void Method_All_out(DataContractEvents_All obj1, out DataContractEvents_All obj2)
        {
            obj2 = obj1;
            return;
        }

        public DataContractEvents_All Method_All_ref(ref DataContractEvents_All obj1)
        {
            return obj1;
        }

        public DataContractEvents_All Method_All(DataContractEvents_All obj1)
        {
            return obj1;
        }

        public void Method_OnSerializing_out(DataContractEvents_OnSerializing obj1, out DataContractEvents_OnSerializing obj2)
        {
            obj2 = obj1;
            return;
        }

        public DataContractEvents_OnSerializing Method_OnSerializing_ref(ref DataContractEvents_OnSerializing obj1)
        {
            return obj1;
        }

        public DataContractEvents_OnSerializing Method_OnSerializing(DataContractEvents_OnSerializing obj1)
        {
            return obj1;
        }

        public void Method_OnSerialized_out(DataContractEvents_OnSerialized obj1, out DataContractEvents_OnSerialized obj2)
        {
            obj2 = obj1;
            return;
        }

        public DataContractEvents_OnSerialized Method_OnSerialized_ref(ref DataContractEvents_OnSerialized obj1)
        {
            return obj1;
        }

        public DataContractEvents_OnSerialized Method_OnSerialized(DataContractEvents_OnSerialized obj1)
        {
            return obj1;
        }

        public void Method_OnDeserializing_out(DataContractEvents_OnDeserializing obj1, out DataContractEvents_OnDeserializing obj2)
        {
            obj2 = obj1;
            return;
        }

        public DataContractEvents_OnDeserializing Method_OnDeserializing_ref(ref DataContractEvents_OnDeserializing obj1)
        {
            return obj1;
        }

        public DataContractEvents_OnDeserializing Method_OnDeserializing(DataContractEvents_OnDeserializing obj1)
        {
            return obj1;
        }

        public void Method_OnDeserialized_out(DataContractEvents_OnDeserialized obj1, out DataContractEvents_OnDeserialized obj2)
        {
            obj2 = obj1;
            return;
        }

        public DataContractEvents_OnDeserialized Method_OnDeserialized_ref(ref DataContractEvents_OnDeserialized obj1)
        {
            return obj1;
        }

        public DataContractEvents_OnDeserialized Method_OnDeserialized(DataContractEvents_OnDeserialized obj1)
        {
            return obj1;
        }
    }
}