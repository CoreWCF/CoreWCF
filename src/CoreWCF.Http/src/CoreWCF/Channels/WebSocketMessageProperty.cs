// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.WebSockets;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class WebSocketMessageProperty
    {
        public const string Name = "WebSocketMessageProperty";
        ReadOnlyDictionary<string, object> properties;

        public WebSocketMessageProperty()
        {
            MessageType = WebSocketDefaults.DefaultWebSocketMessageType;
        }

        internal WebSocketMessageProperty(WebSocketContext context, string subProtocol, WebSocketMessageType incomingMessageType, ReadOnlyDictionary<string, object> properties)
        {
            WebSocketContext = context;
            SubProtocol = subProtocol;
            MessageType = incomingMessageType;
            this.properties = properties;
        }

        public WebSocketContext WebSocketContext { get; }

        public string SubProtocol { get; }

        public WebSocketMessageType MessageType { get; set; }

        public ReadOnlyDictionary<string, object> OpeningHandshakeProperties
        {
            get
            {
                if (properties == null)
                {
                    throw Fx.Exception.AsError(new InvalidOperationException(SR.Format(
                        SR.WebSocketOpeningHandshakePropertiesNotAvailable,
                        "RequestMessage",
                        typeof(HttpResponseMessage).Name,
                        typeof(DelegatingHandler).Name)));
                }

                return properties;
            }
        }

        internal static bool TryGet(MessageProperties properties, out WebSocketMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            property = null;
            object foundProperty;
            if (properties.TryGetValue(Name, out foundProperty))
            {
                property = (WebSocketMessageProperty)foundProperty;
                return true;
            }
            return false;
        }
    }
}