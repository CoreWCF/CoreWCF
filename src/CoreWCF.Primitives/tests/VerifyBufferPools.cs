// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Primitives.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Primitives.Tests
{
    public class VerifyBufferPoolsCreated
    {
        private bool testPassed = true;
        private readonly ITestOutputHelper _output;

        public VerifyBufferPoolsCreated(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void VerifyBufferPoolsCreatedtest()
        {
            for (int i = 80000; i <= 90000; i += 5000)
            {
                BufferManager bufferManager = BufferManager.CreateBufferManager(524288L, i);
                bool flag = BufferManagerTestsCommon.VerifyBufferPoolsCreated(bufferManager);
                testPassed &= flag;
                _output.WriteLine("Correct BufferPools are created for maxbuffersize:{0} {1} ", new object[]
                {
                    i,
                    flag
                });

                Assert.True(testPassed);
            }
        }
    }
}
