// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public static class ServiceBehaviorUseSynchronizationContextTests
    {
        [Fact]
        public static void DefaultValueTest()
        {
            ServiceBehaviorAttribute attribute = new ServiceBehaviorAttribute();
            Assert.True(attribute.UseSynchronizationContext);
        }
    }
}
