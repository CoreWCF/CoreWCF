using ServiceContract;
using System.Threading.Tasks;

namespace Services
{
    public class XmlSerializerContract : IXmlSerializerContract
    {
        // Token: 0x060008D9 RID: 2265 RVA: 0x00020A24 File Offset: 0x0001EC24
        public Task<XmlSerializerPerson> GetPerson()
        {
            Task<XmlSerializerPerson> task = new Task<XmlSerializerPerson>(() => new XmlSerializerPerson());
            task.Start();
            return task;
        }
    }
}
