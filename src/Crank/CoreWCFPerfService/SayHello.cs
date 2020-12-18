using System.Threading.Tasks;

namespace CoreWCFPerfService
{
    public class SayHello : ISayHello
    {
        public string Hello(string name)
        {
            return name;
        }
    }
}
