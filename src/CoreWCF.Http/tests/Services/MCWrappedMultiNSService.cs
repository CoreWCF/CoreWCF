// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public class ServerWrappedMultipleNSService2 : IMCWrappedMultiNS2
    {
        public MC2MultiNS2 M(MCMultiNS2 msg)
        {
            Assert.NotNull(msg);
            return new MC2MultiNS2();
        }
    }

    public class ServerWrappedMultipleNSService3 : IMCWrappedMultiNS3
    {
        public MC2MultiNS3 M(MCMultiNS3 msg)
        {
            Assert.NotNull(msg);
            return new MC2MultiNS3();
        }
    }
}
