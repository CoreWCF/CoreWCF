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
}
