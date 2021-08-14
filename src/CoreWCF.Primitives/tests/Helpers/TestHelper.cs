// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF;
using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace Helpers
{
    public static class TestHelper
    {
        private const string EchoAction = "http://tempuri.org/ISimpleService/Echo";
        private static readonly string s_echoPrefix = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns = ""http://tempuri.org/"">
      <echo>";
        private static readonly string s_echoSuffix = @"</echo>
    </Echo>
  </s:Body>
</s:Envelope>";

        public static Message CreateEchoRequestMessage(string echo)
        {
            string requestMessageStr = s_echoPrefix + echo + s_echoSuffix;
            var xmlDictionaryReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(requestMessageStr), XmlDictionaryReaderQuotas.Max);
            var requestMessage = Message.CreateMessage(xmlDictionaryReader, int.MaxValue, MessageVersion.Soap11);
            requestMessage.Headers.Action = EchoAction;
            return requestMessage;
        }

        internal static System.ServiceModel.Channels.Message ConvertMessage(Message message)
        {
            MemoryStream ms = SerializeMessageToStream(message);
            System.ServiceModel.Channels.Message convertedMessage = DeserialzieMessageFromStream(ms, ConvertMessageVersion(message.Version));
            convertedMessage.Headers.To = message.Headers.To;
            return convertedMessage;
        }

        internal static Message ConvertMessage(System.ServiceModel.Channels.Message message)
        {
            MemoryStream ms = SerializeMessageToStream(message);
            Message convertedMessage = DeserialzieMessageFromStreamAsync(ms, ConvertMessageVersion(message.Version)).Result;
            convertedMessage.Headers.To = message.Headers.To;
            return convertedMessage;
        }

        private static MessageVersion ConvertMessageVersion(System.ServiceModel.Channels.MessageVersion version)
        {
            EnvelopeVersion envelopeVersion = null;
            if (System.ServiceModel.EnvelopeVersion.None.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.None;
            }
            else if (System.ServiceModel.EnvelopeVersion.Soap11.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.Soap11;
            }
            else if (System.ServiceModel.EnvelopeVersion.Soap12.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.Soap12;
            }

            AddressingVersion addressingVersion = null;
            if (System.ServiceModel.Channels.AddressingVersion.None.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.None;
            }
            else if (System.ServiceModel.Channels.AddressingVersion.WSAddressing10.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.WSAddressing10;
            }
            else if (System.ServiceModel.Channels.AddressingVersion.WSAddressingAugust2004.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.WSAddressingAugust2004;
            }

            return MessageVersion.CreateVersion(envelopeVersion, addressingVersion);
        }

        private static System.ServiceModel.Channels.MessageVersion ConvertMessageVersion(MessageVersion version)
        {
            System.ServiceModel.EnvelopeVersion envelopeVersion = null;
            if (EnvelopeVersion.None.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.None;
            }
            else if (EnvelopeVersion.Soap11.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.Soap11;
            }
            else if (EnvelopeVersion.Soap12.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.Soap12;
            }

            System.ServiceModel.Channels.AddressingVersion addressingVersion = null;
            if (AddressingVersion.None.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.None;
            }
            else if (AddressingVersion.WSAddressing10.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.WSAddressing10;
            }
            else if (AddressingVersion.WSAddressingAugust2004.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.WSAddressingAugust2004;
            }

            return System.ServiceModel.Channels.MessageVersion.CreateVersion(envelopeVersion, addressingVersion);
        }

        private static Task<Message> DeserialzieMessageFromStreamAsync(MemoryStream ms, MessageVersion messageVersion)
        {
            var tmebe = new TextMessageEncodingBindingElement
            {
                MessageVersion = messageVersion,
                ReaderQuotas = XmlDictionaryReaderQuotas.Max
            };
            MessageEncoderFactory tmef = tmebe.CreateMessageEncoderFactory();
            return tmef.Encoder.ReadMessageAsync(ms, int.MaxValue);
        }

        private static MemoryStream SerializeMessageToStream(System.ServiceModel.Channels.Message requestMessage)
        {
            var tmebe = new System.ServiceModel.Channels.TextMessageEncodingBindingElement
            {
                MessageVersion = requestMessage.Version,
                ReaderQuotas = XmlDictionaryReaderQuotas.Max
            };
            System.ServiceModel.Channels.MessageEncoderFactory tmef = tmebe.CreateMessageEncoderFactory();
            var ms = new MemoryStream(64 * 1024); // 64K to keep out of LOH
            tmef.Encoder.WriteMessage(requestMessage, ms);
            ms.Position = 0;
            return ms;
        }

        internal static System.ServiceModel.Channels.Message DeserialzieMessageFromStream(MemoryStream ms, System.ServiceModel.Channels.MessageVersion messageVersion)
        {
            var tmebe = new System.ServiceModel.Channels.TextMessageEncodingBindingElement
            {
                MessageVersion = messageVersion,
                ReaderQuotas = XmlDictionaryReaderQuotas.Max
            };
            System.ServiceModel.Channels.MessageEncoderFactory tmef = tmebe.CreateMessageEncoderFactory();
            return tmef.Encoder.ReadMessage(ms, int.MaxValue);
        }

        private static MemoryStream SerializeMessageToStream(Message requestMessage)
        {
            var tmebe = new TextMessageEncodingBindingElement
            {
                MessageVersion = requestMessage.Version,
                ReaderQuotas = XmlDictionaryReaderQuotas.Max
            };
            MessageEncoderFactory tmef = tmebe.CreateMessageEncoderFactory();
            var ms = new MemoryStream(64 * 1024); // 64K to keep out of LOH
            tmef.Encoder.WriteMessageAsync(requestMessage, ms);
            ms.Position = 0;
            return ms;
        }

        internal static void CloseServiceModelObjects(params System.ServiceModel.ICommunicationObject[] objects)
        {
            foreach (System.ServiceModel.ICommunicationObject comObj in objects)
            {
                try
                {
                    if (comObj == null)
                    {
                        continue;
                    }
                    // Only want to call Close if it is in the Opened state
                    if (comObj.State == System.ServiceModel.CommunicationState.Opened)
                    {
                        comObj.Close();
                    }
                    // Anything not closed by this point should be aborted
                    if (comObj.State != System.ServiceModel.CommunicationState.Closed)
                    {
                        comObj.Abort();
                    }
                }
                catch (TimeoutException)
                {
                    comObj.Abort();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    comObj.Abort();
                }
            }
        }

        public static void RegisterApplicationLifetime(this IServiceCollection services)
        {
#if NET5_0 || NETCOREAPP3_1
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostApplicationLifetime, Microsoft.Extensions.Hosting.Internal.ApplicationLifetime>();
            var genericWebHostApplicationLifetimeType =
                typeof(Microsoft.AspNetCore.Hosting.WebHostBuilder).Assembly.GetType(
                    "Microsoft.AspNetCore.Hosting.GenericWebHostApplicationLifetime");
            services.AddSingleton(typeof(Microsoft.AspNetCore.Hosting.IApplicationLifetime), genericWebHostApplicationLifetimeType!);
#else
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IApplicationLifetime, Microsoft.AspNetCore.Hosting.Internal.ApplicationLifetime>();
#endif

        }
    }
}
