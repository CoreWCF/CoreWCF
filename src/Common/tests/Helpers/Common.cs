using System;

namespace Helpers
{
    public class Common
    {
        public static int numThreads = 16;
        public static int requests = 15;
        public static int requestsForSlowService = 20;
        public static int streamBufferSize = 1024;
        public static int stressStreamBufferSize = 40 * 1024;
        public static int defaultBufferSize = 64 * 1024;
        public const string numberOfThreads = "numberOfThreads";
        public const string numberOfRequests = "numberOfRequests";
        public const string serviceAddress = "serviceAddress";
        public const string basicHttpBinding = "basicHttpBinding";
        public const string secureBasicHttpBinding = "secureBasicHttpBinding";
        public const string serverBasicHttpBinding = "serverBasicHttpBinding";
        public const string customBinding = "customBinding";
        public const string byteStreamBinding = "byteStreamBinding";
        public const string largeFile = "LargeFile.txt";
        public static bool testImpersonation = false;
        public static bool testTimeouts = false;
        public static int expectedTimeout = 10;
        public const string serviceReady = "serviceReady";
        public const string clientDone = "clientDone";
        public const string durationExpired = "durationExpired";
        public const string streamingLargeDataServiceSelfHostTestFile = "StreamingLargeDataServiceSelfHost.test";

        public static void FillPatternWithRandomBytes(ref byte[] pattern)
        {
            int seed = (int)DateTime.Now.Ticks;
            Random rand = new Random(seed);
            rand.NextBytes(pattern);
        }
    }
 }

