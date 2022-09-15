// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    public interface IErrorPageWithDetailsService
    {
        [WebGet(UriTemplate = "/errorpage")]
        public void CreatesErrorPage();
    }
}
