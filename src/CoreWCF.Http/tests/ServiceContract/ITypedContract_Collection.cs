using CoreWCF;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [CollectionDataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    [KnownType(typeof(MyCollection))]
    [KnownType(typeof(List<string>))]
    public class MyCollection : CollectionBase
    {
        public int Add(short value) { return (List.Add(value)); }
        public short this[int index]
        {
            get
            {
                return ((short)List[index]);
            }
            set
            {
                List[index] = value;
            }
        }
    }

    [ServiceContract]
    [ServiceKnownType(typeof(MyCollection))]
    [ServiceKnownType(typeof(List<string>))]
    interface ITypedContract_Collection
    {
        [OperationContract]
        ArrayList ArrayListMethod(ArrayList collection);

        [OperationContract]
        Collection<string> CollectionOfStringsMethod(Collection<string> collection);

        [OperationContract]
        MyCollection CollectionBaseMethod(MyCollection collection);
    }
}