// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.DataContractSerialization.AotSmokeTest
{
    /// <summary>
    /// The wire, written by hand.
    /// </summary>
    /// <remarks>
    /// System.ServiceModel would be the obvious client and is not an option: it does not support AOT
    /// either, so a failure there would say nothing about the service. A hand-written envelope over
    /// HttpClient keeps the client out of what is being measured.
    /// </remarks>
    public static class SoapEnvelope
    {
        public const string ContractNamespace = "http://corewcf.example/aot";

        public static byte[] Write(AotXmlObjectSerializer serializer, object graph)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream, Encoding.UTF8, ownsStream: false))
                {
                    serializer.WriteObject(writer, graph);
                }

                return stream.ToArray();
            }
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Only ever handed the reflection-based serializer, deliberately - see ReflectionSerializerBehaviour.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Same.")]
        public static byte[] Write(System.Runtime.Serialization.XmlObjectSerializer serializer, object graph)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream, Encoding.UTF8, ownsStream: false))
                {
                    serializer.WriteObject(writer, graph);
                }

                return stream.ToArray();
            }
        }

        public static object Read(AotXmlObjectSerializer serializer, byte[] document)
        {
            using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(document, XmlDictionaryReaderQuotas.Max))
            {
                return serializer.ReadObject(reader, verifyObjectName: true);
            }
        }

        /// <summary>
        /// The request, written out member by member in the order the serializer expects them.
        /// </summary>
        /// <remarks>
        /// Unordered members sort alphabetically by their wire name, so this is Customer, Discount,
        /// Id, Line, PlacedUtc, Status - not declaration order. Writing it by hand rather than
        /// generating it is the point: it is a document from outside, and the service has to read
        /// what someone else wrote.
        /// </remarks>
        public static string EchoRequest() =>
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
              "<s:Body>" +
                "<Echo xmlns=\"" + ContractNamespace + "\">" +
                  "<order>" +
                    "<Customer>Ada &lt;&amp;&gt; Lovelace</Customer>" +
                    "<Discount>12.5</Discount>" +
                    "<Id>42</Id>" +
                    "<Line>" +
                      "<Quantity>2</Quantity>" +
                      "<Sku>A-1</Sku>" +
                    "</Line>" +
                    "<PlacedUtc>2026-08-09T10:11:12Z</PlacedUtc>" +
                    "<Status>in-progress</Status>" +
                    "<Tags xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\">" +
                      "<a:string>rush</a:string>" +
                      "<a:string>gift</a:string>" +
                    "</Tags>" +
                  "</order>" +
                "</Echo>" +
              "</s:Body>" +
            "</s:Envelope>";

        /// <summary>Checks the response carries the graph back, member by member.</summary>
        public static void AssertEchoResponse(string body)
        {
            Require(body, "<Customer>Ada &lt;&amp;&gt; Lovelace</Customer>");
            Require(body, "<Discount>12.5</Discount>");
            Require(body, "<Id>42</Id>");
            Require(body, "<Sku>A-1</Sku>");
            Require(body, "<Quantity>2</Quantity>");
            Require(body, ">rush<");
            Require(body, ">gift<");
            Require(body, "<PlacedUtc>2026-08-09T10:11:12Z</PlacedUtc>");

            // The one that only an enum table can produce: the wire name, not the CLR name.
            Require(body, "<Status>in-progress</Status>");
        }

        private static void Require(string body, string fragment)
        {
            if (body.IndexOf(fragment, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Response is missing " + fragment);
            }
        }
    }
}
