// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    // These tests compile the code the generator emits, and that code is entitled to net6+ types -
    // DateOnly, TimeOnly, ReferenceEqualityComparer - because CoreWCF.DataContractSerialization.targets
    // gates the generator off below net8.0. Compiling its output against net472 reference assemblies
    // therefore tests a combination that cannot occur, and 44 of the tests fail there.
    //
    // Every test project is still run against every test TFM, so the project has to produce a net472
    // assembly regardless, and with xunit.v3 an assembly containing no tests fails the run with
    // "No test is available in [assembly]". Same shape as CoreWCF.Kafka.Tests, and the same dummy.
    //
    // That the generator contributes nothing on net472 is verified where it is observable:
    // CoreWCF.DataContractSerialization.Tests runs there and reports every case unsupported.
    public class DummyTest
    {
        [Fact(Skip = "Dummy test to prevent test run failure when no other tests are available")]
        public void DummyTestMethod()
        {
        }
    }
}
