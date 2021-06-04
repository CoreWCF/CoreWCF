using System;

namespace CoreWCF.Samples.StandardCommon
{
    public class Settings
    {
        public bool UseHttps { get; set; } = true;
        public Uri basicHttpAddress { get; set; }
        public Uri basicHttpsAddress { get; set; }
        public Uri wsHttpAddress { get; set; }
        public Uri wsHttpAddressValidateUserPassword { get; set; }
        public Uri wsHttpsAddress { get; set; }
        public Uri netTcpAddress { get; set; }

        public Settings SetDetaults(string hostname = "localhost")
        {
            string baseHttpAddress = hostname + ":8088";
            string baseHttpsAddress = hostname + ":8443";
            string baseTcpAddress = hostname + ":8089";

            basicHttpAddress = new Uri($"http://{baseHttpAddress}/basichttp");
            basicHttpsAddress = new Uri($"https://{baseHttpsAddress}/basichttp");
            wsHttpAddress = new Uri($"http://{baseHttpAddress}/wsHttp.svc");
            wsHttpAddressValidateUserPassword = new Uri($"https://{baseHttpsAddress}/wsHttpUserPassword.svc");
            wsHttpsAddress = new Uri($"https://{baseHttpsAddress}/wsHttp.svc");
            netTcpAddress = new Uri($"net.tcp://{baseTcpAddress }/nettcp");
            return this;
        }
    }
}
