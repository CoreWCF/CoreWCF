﻿// Licensed to the .NET Foundation under one or more agreements.
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
    public class DataTypesTest
    {
        private readonly ITestOutputHelper _output;

        public DataTypesTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CollectionsDataContract()
        {
            AppContext.SetSwitch("CoreWCF.RemoveKeyValuePairFromWsdl", true);
            await TestHelper.RunSingleWsdlTestAsync<CollectionsService, ICollectionsService>(new BasicHttpBinding(), _output);
            AppContext.SetSwitch("CoreWCF.RemoveKeyValuePairFromWsdl", false);
        }

        [Fact]
        public async Task PrimitivesDataContract()
        {
            await TestHelper.RunSingleWsdlTestAsync<PrimitivesService, IPrimitivesService>(new BasicHttpBinding(), _output);
        }

        [Fact]
        public async Task ComplexTypesWithCollectionsDataContract()
        {
            await TestHelper.RunSingleWsdlTestAsync<ComplexTypesWithCollectionsService, IComplexTypesWithCollectionsService>(new BasicHttpBinding(), _output);
        }

        [Fact]
        public async Task CollectionOfKeyValuePairDataContract()
        {
            await TestHelper.RunSingleWsdlTestAsync<CollectionOfKeyValuePairDataService, ICollectionOfKeyValuePairDataService>(new BasicHttpBinding(), _output);
        }
    }
}
