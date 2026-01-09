// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Metadata.Tests.Helpers;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class MultipleServicesTest
    {
        private readonly ITestOutputHelper _output;

        public MultipleServicesTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [FactSkipUnsupportedNetVersion]
        public async Task BasicHttpRequestReplyEchoString()
        {
            AppContext.SetSwitch("CoreWCF.RemoveKeyValuePairFromWsdl", true);
            await TestHelper.RunMultipleWsdlTestAsync<SimpleEchoService, CollectionsService, IEchoService, ICollectionsService>(
                new BasicHttpBinding(), new BasicHttpBinding(), _output);
            AppContext.SetSwitch("CoreWCF.RemoveKeyValuePairFromWsdl", false);
        }
    }
}
