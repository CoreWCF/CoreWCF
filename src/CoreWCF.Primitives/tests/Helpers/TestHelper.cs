using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace CoreWCF.Primitives.Tests.Helpers
{
    public static class TestHelper
    {
        private const string EchoAction = "http://tempuri.org/ISimpleService/Echo";
        private static string s_echoPrefix = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns = ""http://tempuri.org/"">
      <echo>";
        private static string s_echoSuffix = @"</echo>
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
    }
}
