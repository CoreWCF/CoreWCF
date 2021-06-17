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
        private readonly ReadOnlyDictionary<string, object> _properties;

        /// <summary>
        /// Initializes a new instance of the WebSocketMessageProperty class.
        /// </summary>
        /// <remarks>Defaults: WebSocketMessageType.Binary</remarks>
        public WebSocketMessageProperty()
        {
            MessageType = WebSocketDefaults.DefaultWebSocketMessageType;
        }

        internal WebSocketMessageProperty(WebSocketContext context, string subProtocol, WebSocketMessageType incomingMessageType, ReadOnlyDictionary<string, object> properties)
        {
            WebSocketContext = context;
            SubProtocol = subProtocol;
            MessageType = incomingMessageType;
            _properties = properties;
        }

        /// <summary>
        /// Gets the web socket context.
        /// </summary>
        public WebSocketContext WebSocketContext { get; }

        /// <summary>
        /// Gets the sub-protocol.
        /// </summary>
        public string SubProtocol { get; }

        /// <summary>
        /// Gets or sets the type of the message. The default is WebSocketMessageType.Binary.
        /// </summary>
        public WebSocketMessageType MessageType { get; set; }

        public ReadOnlyDictionary<string, object> OpeningHandshakeProperties
        {
            get
            {
                if (_properties == null)
                {
                    throw Fx.Exception.AsError(new InvalidOperationException(SR.Format(
                        SR.WebSocketOpeningHandshakePropertiesNotAvailable,
                        "RequestMessage",
                        typeof(HttpResponseMessage).Name,
                        typeof(DelegatingHandler).Name)));
                }

                return _properties;
            }
        }

        internal static bool TryGet(MessageProperties properties, out WebSocketMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            property = null;
            if (properties.TryGetValue(Name, out object foundProperty))
            {
                property = (WebSocketMessageProperty)foundProperty;
                return true;
            }
            return false;
        }
    }
}
