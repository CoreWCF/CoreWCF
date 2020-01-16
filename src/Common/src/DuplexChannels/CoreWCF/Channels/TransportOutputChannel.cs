using System;
using System.Xml;

namespace CoreWCF.Channels
{
    internal abstract class TransportOutputChannel : OutputChannel
    {
        private bool _anyHeadersToAdd;
        private EndpointAddress _to;
        private Uri _via;
        private ToHeader _toHeader;

        protected TransportOutputChannel(IDefaultCommunicationTimeouts timeouts, EndpointAddress to, Uri via, bool manualAddressing, MessageVersion messageVersion)
            : base(timeouts)
        {
            ManualAddressing = manualAddressing;
            MessageVersion = messageVersion;
            _to = to;
            _via = via;

            if (!manualAddressing && _to != null)
            {
                Uri toUri;
                if (_to.IsAnonymous)
                {
                    toUri = MessageVersion.Addressing.AnonymousUri;
                }
                else if (_to.IsNone)
                {
                    toUri = MessageVersion.Addressing.NoneUri;
                }
                else
                {
                    toUri = _to.Uri;
                }

                if (toUri != null)
                {
                    XmlDictionaryString dictionaryTo = new ToDictionary(toUri.AbsoluteUri).To;
                    _toHeader = ToHeader.Create(toUri, dictionaryTo, messageVersion.Addressing);
                }

                _anyHeadersToAdd = _to.Headers.Count > 0;
            }
        }

        protected bool ManualAddressing { get; }

        public MessageVersion MessageVersion { get; }

        public override EndpointAddress RemoteAddress
        {
            get
            {
                return _to;
            }
        }

        public override Uri Via
        {
            get
            {
                return _via;
            }
        }

        protected override void AddHeadersTo(Message message)
        {
            base.AddHeadersTo(message);

            if (_toHeader != null)
            {
                // TODO: Removed performance enhancement to avoid exposing another internal method.
                // Evaluate whether we should do something to bring this back. My thoughts are we 
                // remove the SetToHeader method as we should be using the same mechanism as third
                // parties transports have to use.
                message.Headers.To = _toHeader.To;

                // Original comment and code:
                // we don't use to.ApplyTo(message) since it's faster to cache and
                // use the actual <To> header then to call message.Headers.To = Uri...
                //message.Headers.SetToHeader(toHeader);

                if (_anyHeadersToAdd)
                {
                    _to.Headers.AddHeadersTo(message);
                }
            }
        }

        private class ToDictionary : IXmlDictionary
        {
            private XmlDictionaryString to;

            public ToDictionary(string to)
            {
                this.to = new XmlDictionaryString(this, to, 0);
            }

            public XmlDictionaryString To
            {
                get
                {
                    return to;
                }
            }

            public bool TryLookup(string value, out XmlDictionaryString result)
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                if (value == to.Value)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }

            public bool TryLookup(int key, out XmlDictionaryString result)
            {
                if (key == 0)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }

            public bool TryLookup(XmlDictionaryString value, out XmlDictionaryString result)
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                if (value == to)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }
        }
    }
}
