using System.Threading.Tasks;

namespace CoreWCFPerfService
{
    public class SayHello : ISayHello
    {
        public Task<string> HelloAsync(string name)
        {
            return Task.Factory.StartNew(() => { return string.Format("Hello {0}", name); });
        }
    }
}
