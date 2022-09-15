// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Http.Tests
{
    public class BasicHttpBindingPropertyTests
    {
        [Fact]
        void BasicHttpBindingSecurityModeNonePropertiesPropagated()
        {
            int expectedMaxBufferSize = 7654321;
            int expectedMaxReceivedMessageSize = 87654321;
            string expectedScheme = "http";

            BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
            // Check the default values are as expected
            Assert.Equal(TransferMode.Buffered, binding.TransferMode);
            Assert.Equal(MessageVersion.Soap11, binding.MessageVersion);
            Assert.Equal(65536, binding.MaxReceivedMessageSize);
            Assert.Equal(65536, binding.MaxBufferSize);
            Assert.Equal(expectedScheme, binding.Scheme);

            binding.MaxBufferSize = expectedMaxBufferSize;
            binding.MaxReceivedMessageSize = expectedMaxReceivedMessageSize;
            binding.TransferMode = TransferMode.Streamed;

            BindingElementCollection bindingElements = binding.CreateBindingElements();
            HttpTransportBindingElement htbe = bindingElements.Find<HttpTransportBindingElement>();
            Assert.Equal("CoreWCF.Channels.HttpTransportBindingElement", htbe.GetType().FullName);
            Assert.Equal(expectedMaxBufferSize, htbe.MaxBufferSize);
            Assert.Equal(expectedMaxReceivedMessageSize, htbe.MaxReceivedMessageSize);
            Assert.Equal(expectedScheme, htbe.Scheme);
            Assert.Equal(TransferMode.Streamed, htbe.TransferMode);
            MessageEncodingBindingElement mebe = bindingElements.Find<MessageEncodingBindingElement>();
            Assert.Equal("CoreWCF.Channels.TextMessageEncodingBindingElement", mebe.GetType().FullName);
            Assert.Equal(MessageVersion.Soap11, mebe.MessageVersion);
        }

        [Fact]
        void BasicHttpBindingSecurityModeTransportPropertiesPropagated()
        {
            int expectedMaxBufferSize = 7654321;
            int expectedMaxReceivedMessageSize = 87654321;
            string expectedScheme = "https";

            BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            // Check the default values are as expected
            Assert.Equal(TransferMode.Buffered, binding.TransferMode);
            Assert.Equal(MessageVersion.Soap11, binding.MessageVersion);
            Assert.Equal(65536, binding.MaxReceivedMessageSize);
            Assert.Equal(65536, binding.MaxBufferSize);
            Assert.Equal(expectedScheme, binding.Scheme);

            binding.MaxBufferSize = expectedMaxBufferSize;
            binding.MaxReceivedMessageSize = expectedMaxReceivedMessageSize;
            binding.TransferMode = TransferMode.Streamed;

            BindingElementCollection bindingElements = binding.CreateBindingElements();
            HttpTransportBindingElement htbe = bindingElements.Find<HttpTransportBindingElement>();
            Assert.Equal("CoreWCF.Channels.HttpsTransportBindingElement", htbe.GetType().FullName);
            Assert.Equal(expectedMaxBufferSize, htbe.MaxBufferSize);
            Assert.Equal(expectedMaxReceivedMessageSize, htbe.MaxReceivedMessageSize);
            Assert.Equal(expectedScheme, htbe.Scheme);
            Assert.Equal(TransferMode.Streamed, htbe.TransferMode);
            MessageEncodingBindingElement mebe = bindingElements.Find<MessageEncodingBindingElement>();
            Assert.Equal("CoreWCF.Channels.TextMessageEncodingBindingElement", mebe.GetType().FullName);
            Assert.Equal(MessageVersion.Soap11, mebe.MessageVersion);
        }
    }
}
