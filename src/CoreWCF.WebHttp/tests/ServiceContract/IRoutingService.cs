// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    public interface IRoutingService
    {
        [WebGet(UriTemplate = "/noparam")]
        public void NoParam();

        [WebGet(UriTemplate = "/pathparam/{val}", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string PathParam(string val);

        [WebGet(UriTemplate = "/queryparam?param={val}", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string QueryParam(string val);

        [WebInvoke(Method = "*", UriTemplate = "*", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string Wildcard();

        [WebGet(UriTemplate = "compound/{filename}.{ext}", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string CompoundPath(string filename, string ext);

        [WebGet(UriTemplate = "named/{*val}", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string NamedWildcard(string val);

        [WebGet(UriTemplate = "default/{val=default}", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        public string DefaultValue(string val);
    }
}
