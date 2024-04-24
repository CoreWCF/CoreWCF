// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ClassAccessibilityTests
    {
        [Fact]
        public void InMemorySymmetricSecurityKeyClassIsPublic()
        {
            bool isPublic = typeof(CoreWCF.IdentityModel.Tokens.InMemorySymmetricSecurityKey).IsPublic;

            Assert.True(isPublic);
        }
    }
}
