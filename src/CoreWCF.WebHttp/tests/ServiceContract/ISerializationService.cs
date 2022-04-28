// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Web;

namespace ServiceContract
{
    [ServiceContract]
    public interface ISerializationService
    {
        [WebInvoke(Method = "POST", UriTemplate = "/json", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        public SerializationData SerializeDeserializeJson(SerializationData data);

        [WebInvoke(Method = "POST", UriTemplate = "/xml", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Xml, RequestFormat = WebMessageFormat.Xml)]
        public SerializationData SerializeDeserializeXml(SerializationData data);
    }
}
