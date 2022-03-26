using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreWCF.Samples.StandardCommon
{
    public class Settings
    {
        private const string DefaultHostName = "localhost";
        public bool UseHttps { get; set; } = true;
        public Uri basicHttpAddress { get; set; }
        public Uri basicHttpsAddress { get; set; }
        public Uri wsHttpAddress { get; set; }
        public Uri wsHttpAddressValidateUserPassword { get; set; }
        public Uri wsHttpsAddress { get; set; }
        public Uri netTcpAddress { get; set; }

        public int httpPort { get; set; } = 8088;
        public int httpsPort { get; set; } = 8443;
        public int nettcpPort { get; set; } = 8089;

        public IEnumerable<Uri> GetBaseAddresses(string hostname = DefaultHostName)
        {
            return new[] {
                $"net.tcp://{hostname}:{nettcpPort}/",
                $"http://{hostname}:{httpPort}/",
                $"https://{hostname}:{httpsPort}/" }
            .Select(a => new Uri(a)).ToArray();
        }

        private Uri AddPathPrefix(Uri source, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return source;
            var builder = new UriBuilder(source);
            builder.Path = prefix + builder.Path;
            return builder.Uri;
        }

        public Settings SetDefaults(string hostname = DefaultHostName, string serviceprefix = default)
        {
            string baseHttpAddress = hostname + ":8088";
            string baseHttpsAddress = hostname + ":8443";
            string baseTcpAddress = hostname + ":8089";

            basicHttpAddress = AddPathPrefix(new Uri($"http://{baseHttpAddress}/basichttp"), serviceprefix);
            basicHttpsAddress = AddPathPrefix(new Uri($"https://{baseHttpsAddress}/basichttp"), serviceprefix);
            wsHttpAddress = AddPathPrefix(new Uri($"http://{baseHttpAddress}/wsHttp"), serviceprefix);
            wsHttpAddressValidateUserPassword = AddPathPrefix(new Uri($"https://{baseHttpsAddress}/wsHttpUserPassword"), serviceprefix);
            wsHttpsAddress = AddPathPrefix(new Uri($"https://{baseHttpsAddress}/wsHttp"), serviceprefix);
            netTcpAddress = AddPathPrefix(new Uri($"net.tcp://{baseTcpAddress }/nettcp"), serviceprefix);
            return this;
        }
    }
}
