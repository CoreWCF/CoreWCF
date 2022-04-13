// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Metadata.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class EnumTest
    {
        private readonly ITestOutputHelper _output;

        public EnumTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task BasicHttpRequestEnumType() => await TestHelper.RunSingleWsdlTestAsync<Services.EnumService, ServiceContract.IEnumService>(new BasicHttpBinding(), _output);
    }
}
