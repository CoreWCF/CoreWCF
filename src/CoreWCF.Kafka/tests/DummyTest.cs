// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace CoreWCF.Kafka.Tests
{
    // We can't run the tests in this project on Net472, but we run all test projects against all test TFM's. With the change to xunit.v3, a test project
    // containing no tests will fail the test run with "No test is available in [assembly]". This dummy test class prevents that failure.
    // We should consider adding a more robust solution to this problem, such as only running test projects against compatible TFM's.
    public  class DummyTest
    {
        [Fact(Skip = "Dummy test to prevent test run failure when no other tests are available")]
        public void DummyTestMethod()
        {
        }
    }
}
