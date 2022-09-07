// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using ServiceContract;
using Services;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using CoreWCF.Metadata.Tests.Helpers;

namespace CoreWCF.Metadata.Tests
{
    public class XmlSerializableTests
    {
        private readonly ITestOutputHelper _output;

        public XmlSerializableTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "NetCoreOnly")] // DataSet outputs different Schema on NetFx (WSDL v1.0 ver WSDL v2.0)
        public async Task SystemDataTest()
        {
            await TestHelper.RunSingleWsdlTestAsync<SystemDataService, ISystemDataService>(new BasicHttpBinding(), _output);
        }
    }
}
