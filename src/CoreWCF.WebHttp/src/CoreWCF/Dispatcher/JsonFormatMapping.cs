// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class JsonFormatMapping : MultiplexingFormatMapping
    {
        public static readonly WebContentFormat WebContentFormat = WebContentFormat.Json;

        private static readonly string s_defaultMediaType = JsonGlobals.ApplicationJsonMediaType;
        private static readonly Dictionary<Encoding, MessageEncoder> s_encoders = new Dictionary<Encoding, MessageEncoder>();
        private static readonly object s_thisLock = new object();

        public JsonFormatMapping(Encoding writeEncoding, WebContentTypeMapper contentTypeMapper)
            : base(writeEncoding, contentTypeMapper)
        { }

        public override WebContentFormat ContentFormat => WebContentFormat;

        public override WebMessageFormat MessageFormat => WebMessageFormat.Json;

        public override string DefaultMediaType => s_defaultMediaType;

        protected override MessageEncoder Encoder
        {
            get
            {
                lock (s_thisLock)
                {
                    if (!s_encoders.ContainsKey(writeEncoding))
                    {
                        s_encoders[writeEncoding] = new JsonMessageEncoderFactory(writeEncoding, 0, 0, new XmlDictionaryReaderQuotas(), false).Encoder;
                    }
                }

                return s_encoders[writeEncoding];
            }
        }
    }
}
