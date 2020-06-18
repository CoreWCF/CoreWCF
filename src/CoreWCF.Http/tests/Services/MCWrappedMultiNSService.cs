using System;
using System.Collections.Generic;
using System.Text;
using ServiceContract;
using Xunit;

namespace Services
{
    public class ServerWrappedMultipleNSService : IMCWrappedMultiNS
    {
        public MC2MultiNS M(MCMultiNS msg)
        {
            Assert.NotNull(msg);
            return new MC2MultiNS();
        }
    }
}
