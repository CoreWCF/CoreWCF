// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Queue.Tests.InMemoryQueue;

public class InMemoryQueueBinding : Binding
{
    private readonly InMemoryQueueTransportBindingElement _transport = new();
    private readonly BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement = new();
    private readonly TextMessageEncodingBindingElement _textMessageEncodingBindingElement = new();

    public override string Scheme => "inmem://";

    public override BindingElementCollection CreateBindingElements()
    {
        BindingElementCollection elements = new()
        {
            MessageEncoding switch
            {
                InMemoryMessageEncoding.Binary => _binaryMessageEncodingBindingElement,
                InMemoryMessageEncoding.Text => _textMessageEncodingBindingElement,
                _ => _textMessageEncodingBindingElement
            },
            _transport
        };

        return elements;
    }

    public InMemoryMessageEncoding MessageEncoding
    {
        get => _transport.MessageEncoding;
        set => _transport.MessageEncoding = value;
    }
}
