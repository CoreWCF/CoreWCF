// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using CoreWCF.Channels;
using CoreWCF.Primitives.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Primitives.Tests
{
    public class VerifyBufferUsed
    {
        private bool testPassed = true;
        private static byte[] buffer;
        private ITestOutputHelper _output;

        public VerifyBufferUsed(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void VerifyBufferUsedbyThreadsTest()
        {
            for (int i = 80000; i <= 90000; i += 5000)
            {
                _output.WriteLine("Training Buffer of size = " + i);
                for (int j = 63; j <= BufferManagerTestsCommon.TrainingCount; j++)
                {
                    BufferPoolAffinitize(j, i);
                }
            }

            Assert.True(testPassed);
        }

        private void BufferPoolAffinitize(int trainingCount, int bufferSize)
        {
            BufferManager bufferManager = BufferManager.CreateBufferManager(524288L, bufferSize);
            buffer = null;
            for (int i = 0; i < trainingCount; i++)
            {
                buffer = bufferManager.TakeBuffer(bufferSize);
                bufferManager.ReturnBuffer(buffer);
            }

            _output.WriteLine("Same buffers returned for bufferSize={0} and  training Count={1} : ", new object[]
            {
                bufferSize,
                trainingCount
            });

            Thread thread = new Thread(delegate ()
            {
                byte[] array = bufferManager.TakeBuffer(bufferSize);
                bool flag = array == buffer;
                if (bufferSize < BufferManagerTestsCommon.LargeBufferLimit && trainingCount == BufferManagerTestsCommon.TrainingCount)
                {
                    testPassed &= !flag;
                }
                else
                {
                    testPassed &= flag;
                }

                _output.WriteLine(flag.ToString());
            });

            thread.Start();
            thread.Join();
        }
    }
}

