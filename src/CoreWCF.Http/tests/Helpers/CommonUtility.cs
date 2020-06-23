using System;

namespace Helpers
{
    public class CommonUtility
    {
        public static string CreateInterestingString(int length)
        {
            char[] chars = new char[length];
            int index = 0;

            // Arrays of odd length will start with a single char.
            // The rest of the entries will be surrogate pairs.
            if (length % 2 == 1)
            {
                chars[index] = 'a';
                index++;
            }

            // Fill remaining entries with surrogate pairs
            int seed = DateTime.Now.Millisecond;
            Random rand = new Random(seed);
            char highSurrogate;
            char lowSurrogate;

            while (index < length)
            {
                highSurrogate = Convert.ToChar(rand.Next(0xD800, 0xDC00));
                lowSurrogate = Convert.ToChar(rand.Next(0xDC00, 0xE000));

                chars[index] = highSurrogate;
                ++index;
                chars[index] = lowSurrogate;
                ++index;
            }

            return new string(chars, 0, chars.Length);
        }
    }

    public class CommonConstants
    {
        public const string HttpTransport = "http";
        public const string NetTcpTransport = "net.tcp";
        public const string NetPipeTransport = "net.pipe";
        public const string NetMsmqTransport = "net.msmq";
        public const string HttpsTransport = "https";
        public const string ServiceReady = "ServiceReady";
        public const string ClientDone = "ClientDone";
        public const string ServiceAddress = "serviceAddress";
        public const string CompressionEnabledHttpWebhostService = "CompressionEnabledHttpWebhostService.test";
        public const string CompressionEnabledNetTcpSelfhostService = "CompressionEnabledNetTcpSelfhostService.test";
    }
}