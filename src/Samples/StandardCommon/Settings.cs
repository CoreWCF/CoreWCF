namespace StandardCommon
{
    public class Settings
    {
        public bool UseHttps { get; set; } = true;
        public string basicHttpAddress { get; set; }
        public string basicHttpsAddress { get; set; }
        public string wsHttpAddress { get; set; }
        public string wsHttpAddressValidateUserPassword { get; set; }
        public string wsHttpsAddress { get; set; }
        public string netTcpAddress { get; set; }

        public Settings SetDetaults(string hostname = "localhost")
        {
            string baseHttpAddress = hostname + ":8088";
            string baseHttpsAddress = hostname + ":8443";
            string baseTcpAddress = hostname + ":8089";

            basicHttpAddress = $"http://{baseHttpAddress}/basichttp";
            basicHttpsAddress = $"https://{baseHttpsAddress}/basichttp";
            wsHttpAddress = $"http://{baseHttpAddress}/wsHttp.svc";
            wsHttpAddressValidateUserPassword = $"https://{baseHttpsAddress}/wsHttpUserPassword.svc";
            wsHttpsAddress = $"https://{baseHttpsAddress}/wsHttp.svc";
            netTcpAddress = $"net.tcp://{baseTcpAddress }/nettcp";
            return this;
        }
    }
}
