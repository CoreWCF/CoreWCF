using ServiceContract;
using System.Collections;
using System.Collections.ObjectModel;

namespace Services
{
    public class TypedContract_CollectionService : ITypedContract_Collection
    {
        public ArrayList ArrayListMethod(ArrayList collection)
        {
            return (ArrayList)collection.Clone();
        }

        public Collection<string> CollectionOfStringsMethod(Collection<string> collection)
        {
            Collection<string> returnList = new Collection<string>();
            foreach (string s in collection)
            {
                returnList.Add(s);
            }

            return returnList;
        }

        public MyCollection CollectionBaseMethod(MyCollection collection)
        {
            MyCollection returnList = new MyCollection();
            foreach (short i in collection)
            {
                returnList.Add(i);
            }

            return returnList;
        }
    }
}