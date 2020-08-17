using System;
using System.Threading;

namespace Services
{
    public class SM_767311Service : ServiceContract.ISyncService
    {
        public string EchoString(string s)
        {
            Console.WriteLine("In EchoString");
            Console.WriteLine(s);
            Console.WriteLine("(Waiting) on Server.....");
            Thread.CurrentThread.Join(5000);
            Console.WriteLine("Sending response");
            string response = "Async call was valid";
            return response;
        }
    }
}
