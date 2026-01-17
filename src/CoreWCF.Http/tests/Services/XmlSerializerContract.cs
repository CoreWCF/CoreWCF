using ServiceContract;
using System.Threading.Tasks;

namespace Services
{
    public class XmlSerializerContract : IXmlSerializerContract
    {
        public Task<XmlSerializerPerson> GetPerson()
        {
            Task<XmlSerializerPerson> task = new Task<XmlSerializerPerson>(() => new XmlSerializerPerson());
            task.Start();
            return task;
        }
    }
}
