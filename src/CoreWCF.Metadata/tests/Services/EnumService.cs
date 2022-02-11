// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ServiceContract;

namespace Services
{
    public class EnumService : IEnumService
    {
        public void AcceptWrapped(TestWrappedEnum accept) => throw new NotImplementedException();
        public TestWrappedEnum RequestWrapped() => throw new NotImplementedException();
    }
}
