// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.XmlDiff;
using CoreWCF;
using CoreWCF.Channels;

namespace Helpers
{
    public class InterestingMessageSet : IEnumerator<Message>, IEnumerable<Message>
    {
        IEnumerator<Message> IEnumerable<Message>.GetEnumerator()
        {
            return this;
        }

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        public MessageVersion[] Versions
        {
            get
            {
                return versions;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                versions = value;
            }
        }

        public string[] Actions
        {
            get
            {
                return actions;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                actions = value;
            }
        }

        public string[] Bodies
        {
            get
            {
                return bodies;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                if (types != null && value != null)
                {
                    CheckValidTypes(types);
                }

                bodies = value;
            }
        }

        public MessageType[] Types
        {
            get
            {
                return types;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                if (bodies != null && value != null)
                {
                    CheckValidTypes(value);
                }

                types = value;
            }
        }

        public long[] BodySizes
        {
            get
            {
                return bodySizes;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                if (bodies != null)
                {
                    throw new InvalidOperationException();
                }

                bodySizes = value;
            }
        }

        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public int MaxBodyComplexity
        {
            get
            {
                return maxBodyComplexity;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                if (bodies != null)
                {
                    throw new InvalidOperationException();
                }

                maxBodyComplexity = value;
            }
        }

        public int[] Headers
        {
            get
            {
                return headers;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                headers = value;
            }
        }

        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public int MaxHeaderComplexity
        {
            get
            {
                return maxHeaderComplexity;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                maxHeaderComplexity = value;
            }
        }

        // Whether the XML of the message should have embedded XML comments added
        public bool AddComments
        {
            get
            {
                return addComments;
            }
            set
            {
                if (mp != null)
                {
                    throw new InvalidOperationException();
                }

                addComments = value;
            }
        }

        public InterestingMessageSet()
        {
        }

        Message IEnumerator<Message>.Current
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("Object disposed!");
                }

                if (atEnd)
                {
                    throw new InvalidOperationException();
                }

                if (mp == null)
                {
                    throw new InvalidOperationException();
                }

                return mp.CreateMessage();
            }
        }

        public object Current
        {
            get
            {
                return ((IEnumerator<Message>)this).Current;
            }
        }

        public MessageParameters CurrentParameters
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("Object disposed!");
                }

                if (atEnd)
                {
                    throw new InvalidOperationException();
                }

                if (mp == null)
                {
                    throw new InvalidOperationException();
                }

                return new MessageParameters(mp);
            }
        }

        public void Dispose()
        {
            disposed = true;
            Reset();
        }

        public void Reset()
        {
            mp = null;
            atEnd = false;
        }

        public bool Skip(int n, bool wrapAround)
        {
            bool didNotHitEnd = true;

            for (int i = 0; i < n; i++)
            {
                if (!MoveNext())
                {
                    didNotHitEnd = false;

                    if (wrapAround)
                    {
                        Reset();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return didNotHitEnd;
        }

        public bool MoveNext()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Object disposed!");
            }

            if (mp == null)
            {
                versionIndex = actionIndex = bodyIndex = bodySizeIndex = headersIndex = 0;
                typeIndex = 0;
                mp = new MessageParameters
                {
                    addComments = addComments,
                    version = versions[0],
                    action = actions[0],
                    bodyContentSize = bodySizes[0],
                    headers = headers[0]
                };

                if (bodies != null)
                {
                    mp.type = legalTypesForCustomBodies[0];
                    mp.body = bodies[0];
                }
            }
            else
            {
                MoveNextWithoutChecking();
            }

            // While the resultant combination is invalid, skip over it.
            // Of course we stop if we reach the end.
            while
            (
              !atEnd && (false
              || ((mp.type == MessageType.Empty) && (mp.bodyContentSize > 0))
              || ((mp.type == MessageType.Empty) && (mp.bodyComplexity > 1))
              || TypeNotAllowed(mp.type)
            ))
            {
                MoveNextWithoutChecking();
            }

            return !atEnd;
        }

        private bool TypeNotAllowed(MessageType foo)
        {
            if (Types == null)
            {
                return false;
            }

            foreach (MessageType x in Types)
            {
                if (foo == x)
                {
                    return false;
                }
            }

            return true;
        }

        private void MoveNextWithoutChecking()
        {
            if (bodies == null)
            {
                // Allow all valid types.
                // Empty, Fault, XmlFormatterObject, Streamed, Custom, DefaultBuffered, BufferedBuffered, BufferedBodyBuffered
                if (mp.type == MessageType.BufferedBodyBuffered)
                {
                    mp.type = MessageType.Empty;
                }
                else
                {
                    mp.type++;
                    return;
                }

                if (bodySizeIndex >= bodySizes.Length)
                {
                    mp.bodyContentSize = bodySizes[0];
                    bodySizeIndex = 1;
                }
                else
                {
                    mp.bodyContentSize = bodySizes[bodySizeIndex];
                    bodySizeIndex++;
                    return;
                }

                if (mp.bodyComplexity >= maxBodyComplexity)
                {
                    mp.bodyComplexity = 1;
                }
                else
                {
                    mp.bodyComplexity++;
                    return;
                }
            }
            else
            {
                if (typeIndex >= legalTypesForCustomBodies.Length)
                {
                    mp.type = legalTypesForCustomBodies[0];
                    typeIndex = 1;
                }
                else
                {
                    mp.type = legalTypesForCustomBodies[typeIndex];
                    typeIndex++;
                    return;
                }

                if (bodyIndex >= bodies.Length)
                {
                    mp.body = bodies[0];
                    bodyIndex = 1;
                }
                else
                {
                    mp.body = bodies[bodyIndex];
                    bodyIndex++;
                    return;
                }
            }

            if (versionIndex >= versions.Length)
            {
                mp.version = versions[0];
                versionIndex = 1;
            }
            else
            {
                mp.version = versions[versionIndex];
                versionIndex++;
                return;
            }

            if (actionIndex >= actions.Length)
            {
                mp.action = actions[0];
                actionIndex = 1;
            }
            else
            {
                mp.action = actions[actionIndex];
                actionIndex++;
                return;
            }

            if (headersIndex >= headers.Length)
            {
                mp.headers = headers[0];
                headersIndex = 1;
            }
            else
            {
                mp.headers = headers[headersIndex];
                headersIndex++;
                return;
            }

            if (mp.headerComplexity >= maxHeaderComplexity)
            {
                mp.headerComplexity = 1;
                atEnd = true;
                return;
            }
            else
            {
                mp.headerComplexity++;
                return;
            }
        }

        private void CheckValidTypes(MessageType[] types)
        {
            for (int i = 0; i < types.Length; i++)
            {
                int j;

                for (j = 0; j < legalTypesForCustomBodies.Length; j++)
                {
                    if (types[i] == legalTypesForCustomBodies[j])
                    {
                        break;
                    }
                }

                if (j == legalTypesForCustomBodies.Length)
                {
                    throw new InvalidOperationException("Not a legal type for custom bodies!");
                }
            }
        }

        private MessageVersion[] versions = { /* MessageVersion.Soap11WSAddressing10,*/ MessageVersion.Soap12WSAddressing10 };
        private string[] actions = { MessageTestUtilities.SampleAction };
        private string[] bodies = null;
        private MessageType[] types = null;

        // Note: I pulled these default numbers out of thin air.
        private long[] bodySizes = { 0, 1, 100, 10000 };
        private int maxBodyComplexity = 2;
        private int[] headers = { 0, 1, 10, 100 };
        private int maxHeaderComplexity = 2;
        private bool addComments = true;
        private MessageParameters mp = null;
        private bool disposed = false;
        private bool atEnd = false;
        private readonly MessageType[] legalTypesForCustomBodies = { MessageType.Streamed, MessageType.Custom, MessageType.DefaultBuffered, MessageType.BufferedBuffered };
        private int typeIndex = 0;
        private int versionIndex, actionIndex, bodyIndex, bodySizeIndex, headersIndex;
    }

    public enum MessageType { Empty, Fault, XmlFormatterObject, BodyWriter, Streamed, /* Buffered, */ Custom, DefaultBuffered, BufferedBuffered, BufferedBodyBuffered }

    public class MessageParameters
    {
        public string body;
        public MessageType type;
        public MessageVersion version;
        public string action;

        // Comments only applies to certain types of custom message classes
        // It doesn't apply to Empty, Fault, BufferedBodyBuffered, or XmlFormatterObject
        public bool addComments = true;

        public int headers;
        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public int headerComplexity;

        public long bodyContentSize;
        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public int bodyComplexity;

        public MessageParameters()
        {
            body = null;
            type = MessageType.Empty;

            headers = 0;
            headerComplexity = 1;

            bodyContentSize = 0;
            bodyComplexity = 1;

            version = MessageVersion.Soap12WSAddressing10;
            action = MessageTestUtilities.SampleAction;
        }

        public MessageParameters(MessageParameters oneToCopy)
        {
            body = oneToCopy.body;
            type = oneToCopy.type;
            version = oneToCopy.version;
            action = oneToCopy.action;
            headers = oneToCopy.headers;
            headerComplexity = oneToCopy.headerComplexity;
            bodyContentSize = oneToCopy.bodyContentSize;
            bodyComplexity = oneToCopy.bodyComplexity;
            addComments = oneToCopy.addComments;
        }

        // create a message as specified in parameters
        // Note that certain types ignore certain other parameters.
        // E.g., empty message ignores body size/complexity,
        public Message CreateMessage()
        {
            Message message;
            Message model;
            MessageBuffer b;
            object bodyObject;

            if (body == null)
            {
                switch (type)
                {
                    case MessageType.Empty:
                        message = Message.CreateMessage(version, action);
                        break;
                    case MessageType.Streamed:
                        model = new CustomGeneratedMessage(this);
                        StringWriter s1 = new StringWriter();
                        model.WriteMessage(new XmlTextWriter(s1));
                        message = Message.CreateMessage(new XmlTextReader(new StringReader(s1.ToString())), int.MaxValue, version);
                        break;
                    case MessageType.XmlFormatterObject:
                        bodyObject = new GeneratedSerializableObject(bodyComplexity, bodyContentSize);
                        message = Message.CreateMessage(version, action, bodyObject);
                        break;
                    case MessageType.BodyWriter:
                        message = Message.CreateMessage(version, action, new CustomGeneratedBodyWriter(this));
                        break;
                    case MessageType.DefaultBuffered:
                        model = new CustomGeneratedMessage(this);
                        b = model.CreateBufferedCopy(int.MaxValue);
                        message = b.CreateMessage();
                        break;
                    case MessageType.BufferedBodyBuffered:
                        bodyObject = new GeneratedSerializableObject(bodyComplexity, bodyContentSize);
                        model = Message.CreateMessage(version, action, bodyObject);
                        b = model.CreateBufferedCopy(int.MaxValue);
                        message = b.CreateMessage();
                        break;
                    case MessageType.BufferedBuffered:
                        model = new CustomGeneratedMessage(this);
                        MessageEncoder e = (new TextMessageEncodingBindingElement(model.Version, Encoding.UTF8)).CreateMessageEncoderFactory().Encoder;
                        model = e.ReadMessage(e.WriteMessage(model, int.MaxValue, new SimpleBufferManager(), 0), new SimpleBufferManager());
                        b = model.CreateBufferedCopy(int.MaxValue);
                        message = b.CreateMessage();
                        break;
                    case MessageType.Custom:
                        message = new CustomGeneratedMessage(this);
                        break;
                    case MessageType.Fault:
                        MessageFault fault;

                        if (bodyComplexity > 1)
                        {
                            var translations = new Collection<FaultReasonText>
                            {
                                new FaultReasonText("Reason: auto-generated fault for testing.", "en-US"),
                                new FaultReasonText("Raison: auto-generat error pour examiner.", "fr")
                            };
                            var reason = new FaultReason(translations);
                            object detailObject = new GeneratedSerializableObject(bodyComplexity, bodyContentSize);
                            fault = MessageFault.CreateFault(new FaultCode("SomeFaultSubCode"), reason, detailObject, new DataContractSerializer(typeof(GeneratedSerializableObject)), "", "");
                        }
                        else
                        {
                            FaultReason reason = new FaultReason("Reason: auto-generated fault for testing.");
                            fault = MessageFault.CreateFault(new FaultCode("SomeFaultSubCode"), reason, "", new DataContractSerializer(typeof(string)), "", "");
                        }

                        message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, fault, "http://www.w3.org/2005/08/addressing/fault");
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
            else
            {
                switch (type)
                {
                    case MessageType.Custom:
                        message = new CustomStringMessage(this);
                        break;
                    case MessageType.Streamed:
                        model = new CustomStringMessage(this);
                        StringWriter s1 = new StringWriter();
                        model.WriteMessage(new XmlTextWriter(s1));
                        message = Message.CreateMessage(new XmlTextReader(new StringReader(s1.ToString())), int.MaxValue, version);
                        break;
                    case MessageType.DefaultBuffered:
                        model = new CustomStringMessage(this);
                        b = model.CreateBufferedCopy(int.MaxValue);
                        message = b.CreateMessage();
                        break;
                    case MessageType.BufferedBuffered:
                        model = new CustomStringMessage(this);
                        MessageEncoder e = (new TextMessageEncodingBindingElement(model.Version, Encoding.UTF8)).CreateMessageEncoderFactory().Encoder;
                        model = e.ReadMessage(e.WriteMessage(model, int.MaxValue, new SimpleBufferManager(), 0), new SimpleBufferManager());
                        b = model.CreateBufferedCopy(int.MaxValue);
                        message = b.CreateMessage();
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            for (int i = 0; i < headers; i++)
            {
                message.Headers.Add(new CustomGeneratedHeader(i, headerComplexity));
            }

            return message;
        }
    }

    public class MessageTestUtilities
    {
        public static CustomBinding INullBinding = new CustomBinding();

        public const string ServiceAddressPath = "";
        public const string SampleAction = "http://www.example.org/action";
        public const string SampleNamespace = "http://www.example.org/namespace";

        // Simulates a message being sent from a client and received on a server
        public static Message SendAndReceiveMessage(Message toSend)
        {
            MessageEncoder encoder;
            if (toSend.Version.Envelope == EnvelopeVersion.Soap11)
            {
                encoder = new TextMessageEncodingBindingElement(toSend.Version, Encoding.UTF8).CreateMessageEncoderFactory().Encoder;
            }
            else
            {
                encoder = new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;
            }

            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);
            ArraySegment<byte> encodedMessage = encoder.WriteMessage(toSend, int.MaxValue, bufferManager);
            Message r = encoder.ReadMessage(encodedMessage, bufferManager);
            return r;
        }

        // parse string as XML body to create message
        public static Message CreateMessageBodyFromString(MessageVersion v, string action, string xmlBody)
        {
            return new CustomStringMessage(v, action, xmlBody);
        }

        // Save a Message to a file as XML.
        public static void SaveMessageToFile(string filename, Message message)
        {
            XmlTextWriter x = new XmlTextWriter(filename, null);
            message.WriteMessage(x);
            x.Close();
        }

        // compare content of two entire messages
        public static bool AreMessagesEqual(Message one, Message two)
        {
            StringWriter s1 = new StringWriter();
            StringWriter s2 = new StringWriter();
            XmlWriter w1 = new XmlTextWriter(s1);
            XmlWriter w2 = new XmlTextWriter(s2);
            one.WriteMessage(w1);
            two.WriteMessage(w2);
            w1.Close();
            w2.Close();

            return AreXmlStringsEqual(s1.ToString(), s2.ToString());
        }

        public static bool AreXmlStringsEqual(string fc1, string fc2)
        {
            return AreXmlReadersEqual(new XmlTextReader(new StringReader(fc1)), new XmlTextReader(new StringReader(fc1)));
        }

        public static bool AreXmlReadersEqual(XmlReader fc1, XmlReader fc2)
        {
            return new XmlDiff().Compare(fc1, fc2);
        }

        public static string GenerateData(int dataSize)
        {
            return new string('x', dataSize);
        }

        public static bool AreBodiesEqual(Message one, Message two)
        {
            if (one.IsEmpty || two.IsEmpty)
            {
                return one.IsEmpty == two.IsEmpty;
            }
            return AreXmlReadersEqual(one.GetReaderAtBodyContents(), two.GetReaderAtBodyContents());
        }

        public static bool AreBodiesEqual(Message one, Message two, bool onlySubtreeOfOne, bool onlySubtreeOfTwo)
        {
            if (one.IsEmpty || two.IsEmpty)
            {
                return one.IsEmpty == two.IsEmpty;
            }
            return AreXmlReadersEqual(one.GetReaderAtBodyContents(), two.GetReaderAtBodyContents(), onlySubtreeOfOne, onlySubtreeOfTwo);
        }

        public static bool AreXmlReadersEqual(XmlReader one, XmlReader two, bool onlySubtreeOfOne, bool onlySubtreeOfTwo)
        {
            XmlReader xmlReader = one;
            XmlReader fc = two;

            if (onlySubtreeOfOne)
            {
                one.MoveToContent();
                xmlReader = one.ReadSubtree();
            }
            if (onlySubtreeOfTwo)
            {
                two.MoveToContent();
                fc = two.ReadSubtree();
            }

            return AreXmlReadersEqual(xmlReader, fc);
        }
    }

    public class CustomGeneratedHeader : MessageHeader
    {
        private readonly int id;
        private readonly int complexity;
        private readonly string name;
        private readonly string namespaceString = MessageTestUtilities.SampleNamespace;

        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public CustomGeneratedHeader(int id, int complexity)
        {
            this.id = id;
            this.complexity = ((complexity > 0) ? complexity : 1);
            name = "CustomGeneratedHeader-Number" + id;
        }

        public override bool Relay
        {
            get { return false; }
        }

        public override bool MustUnderstand
        {
            get { return false; }
        }

        public override string Name
        {
            get { return name; }
        }

        public override string Namespace
        {
            get { return namespaceString; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion version)
        {
            for (int i = 0; i < complexity; i++)
            {
                writer.WriteStartElement(("nestedSubtag" + i));
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteEndElement();
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteElementString(("nonNestedSubtag" + i), MessageTestUtilities.GenerateData(complexity));
            }
        }
    }

    public class CustomGeneratedMessage : Message
    {
        private readonly MessageVersion version;
        private readonly MessageHeaders headers;
        private readonly MessageProperties properties;
        private readonly int complexity;
        private readonly long size;
        private readonly bool addComments = false;

        public CustomGeneratedMessage(MessageParameters howToCreate)
            :
        this(howToCreate.version, howToCreate.action, howToCreate.bodyComplexity, howToCreate.bodyContentSize)
        {
            addComments = howToCreate.addComments;
        }

        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public CustomGeneratedMessage(MessageVersion version, string action, int bodyComplexity, long bodyContentSize)
        {
            this.version = version;
            headers = new MessageHeaders(version);
            Headers.Action = action;
            properties = new MessageProperties();
            complexity = bodyComplexity;
            size = bodyContentSize;

            size = ((size > 0) ? size : 0);
            complexity = ((complexity > 0) ? complexity : 1);
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return headers;
            }
        }

        public override MessageProperties Properties
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return properties;
            }
        }

        public override MessageVersion Version
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return version;
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("", "MessageClosed.");
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteStartElement(("nestedSubtag" + i));
                if (addComments)
                {
                    writer.WriteComment("Some after-open-tag comment" + i);
                }
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteEndElement();
                if (addComments)
                {
                    writer.WriteComment("Some after-end-tag comment" + i);
                }
            }

            long dataSizeLong = size / complexity;
            int dataSize = ((dataSizeLong > int.MaxValue) ? int.MaxValue : ((int)dataSizeLong));

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteElementString(("nonNestedSubtag" + i), MessageTestUtilities.GenerateData(dataSize));
            }

            if (addComments)
            {
                writer.WriteComment("Some comment");
            }
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            base.OnWriteStartEnvelope(writer);
            if (addComments)
            {
                writer.WriteComment("Post-header open tag comment");
            }
        }
    }

    public class CustomStringMessage : Message
    {
        private readonly MessageVersion version;
        private readonly MessageHeaders headers;
        private readonly MessageProperties properties;
        private readonly XmlReader bodyReader;
        private readonly bool addComments = false;

        public CustomStringMessage(MessageParameters howToCreate)
            : this(howToCreate.version, howToCreate.action, howToCreate.body)
        {
            addComments = howToCreate.addComments;
        }

        public CustomStringMessage(MessageVersion version, string action, string body)
        {
            this.version = version;
            headers = new MessageHeaders(version);
            Headers.Action = action;
            properties = new MessageProperties();
            _ = CreateMessage(version, action, body);
            bodyReader = new XmlTextReader(body, XmlNodeType.Element, null);
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return headers;
            }
        }

        public override MessageProperties Properties
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return properties;
            }
        }

        public override MessageVersion Version
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("", "MessageClosed.");
                }

                return version;
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("", "MessageClosed.");
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (addComments)
            {
                writer.WriteComment("some comment");
            }

            while (!bodyReader.EOF)
            {
                writer.WriteNode(bodyReader, false);
                if (addComments)
                {
                    writer.WriteComment("some comment");
                }
            }

            bodyReader.Close();
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            base.OnWriteStartEnvelope(writer);
        }

        protected override XmlDictionaryReader OnGetReaderAtBodyContents()
        {
            return XmlDictionaryReader.CreateDictionaryReader(bodyReader);
        }
    }

    public class CustomGeneratedBodyWriter : BodyWriter
    {
        private readonly int complexity;
        private readonly long size;
        private readonly bool addComments = false;

        public CustomGeneratedBodyWriter(MessageParameters howToCreate)
            :
        this(howToCreate.bodyComplexity, howToCreate.bodyContentSize)
        {
            addComments = howToCreate.addComments;
        }

        public CustomGeneratedBodyWriter(int bodyComplexity, long bodyContentSize)
            : base(false)
        {
            complexity = bodyComplexity;
            size = bodyContentSize;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            WriteBody(writer);
        }

        public void WriteBody(XmlWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteStartElement(("nestedSubtag" + i));
                if (addComments)
                {
                    writer.WriteComment("Some after-open-tag comment" + i);
                }
            }

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteEndElement();
                if (addComments)
                {
                    writer.WriteComment("Some after-end-tag comment" + i);
                }
            }

            long dataSizeLong = size / complexity;
            int dataSize = ((dataSizeLong > int.MaxValue) ? int.MaxValue : ((int)dataSizeLong));

            for (int i = 0; i < complexity; i++)
            {
                writer.WriteElementString(("nonNestedSubtag" + i), MessageTestUtilities.GenerateData(dataSize));
            }

            if (addComments)
            {
                writer.WriteComment("Some comment");
            }
        }
    }

    [Serializable]
    [DataContract]
    public class GeneratedSerializableObject
    {
        private int _complexity;

        [DataMember]
        public long Size { get; set; }

        [DataMember]
        public int Complexity
        {
            get
            {
                return _complexity;
            }
            set
            {
                _complexity = value;
            }
        }

        // Complexity is an abstract measure that corresponds to general
        // fuzziness of a message:  attributes per element, children per
        // element, depth of child-element nesting, etc..
        public GeneratedSerializableObject(int complexity, long size)
        {
            Size = ((size > 0) ? size : 0);
            Complexity = ((complexity > 0) ? complexity : 1);

            GeneratedSerializableObject pointer = this;

            long dataSizeLong = size / complexity;
            int dataSize = ((dataSizeLong > int.MaxValue) ? int.MaxValue : ((int)dataSizeLong));

            a = (complexity < 2) ? null : new GeneratedSerializableObject(complexity - 1, dataSize * (complexity - 1));
            b = MessageTestUtilities.GenerateData(dataSize);
        }

        public GeneratedSerializableObject()
        {
        }

        public override bool Equals(object obj)
        {
            // Check for null values and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            GeneratedSerializableObject foo = (GeneratedSerializableObject)obj;

            return
            (
              (b == foo.b) &&
              (
                (
                  (a == null) &&
                  (foo.a == null)
                ) ||
                (
                  (a != null) &&
                  a.Equals(foo.a)
                )
              )
            );
        }

        public override int GetHashCode()
        {
            int r = 0;

            if (a != null)
            {
                r ^= a.GetHashCode();
            }

            if (b != null)
            {
                r ^= b.GetHashCode();
            }

            return r;
        }

        [DataMember]
        public GeneratedSerializableObject a = null;

        [DataMember]
        public string b = "";
    }

    public class SimpleBufferManager : BufferManager
    {
        public override byte[] TakeBuffer(int bufferSize)
        {
            return new byte[bufferSize];
        }

        public override void ReturnBuffer(byte[] buffer) { }
        public override void Clear() { }
    }
}
