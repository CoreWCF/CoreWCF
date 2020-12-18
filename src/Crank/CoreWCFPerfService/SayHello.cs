using System.Threading.Tasks;

namespace CoreWCFPerfService
{
    public class SayHello : ISayHello
    {
        public string HelloAsync(string name)
        {
            return name;
        }
    }
}
