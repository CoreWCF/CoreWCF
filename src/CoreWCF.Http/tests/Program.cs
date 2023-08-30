using System.Net;

namespace CoreWCF.Http.Tests
{
    /// <summary>
    /// Required because `<GenerateProgramFile />` is set to `false` to
    /// accommodate differences in behavior between `netcoreapp2.1` and
    /// `netcoreapp3.1`
    /// </summary>
    internal static class Program
    {
        public static void Main(params string[] args)
        {
#if !DEBUG            
            ServicePointManager.MaxServicePointIdleTime = 1000 * 5;
#endif            
        }
    }
}
