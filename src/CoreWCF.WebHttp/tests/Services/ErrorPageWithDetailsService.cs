// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace Services
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class ErrorPageWithDetailsService : ServiceContract.IErrorPageWithDetailsService
    {
        public void CreatesErrorPage() => throw new System.Exception("An error occurred");
    }
}
