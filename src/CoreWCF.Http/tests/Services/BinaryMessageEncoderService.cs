using ServiceContract;
using System.IO;

namespace Services
{
    public class BinaryMessageEncoderService : IBinaryMessageEncoderService
    {
        public Stream GetStream()
        {
            try
            {
                return new MemoryStream(new byte[100 * 1024]);
            }
            catch (IOException ex)
            {
                throw ex;
            }
        }

        public string EchoString(string input)
        {
            return input;
        }
    }
}
