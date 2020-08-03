using System;
using System.Collections;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml;
using System.Xml.Serialization;

namespace ClientContract
{
    [ServiceContract]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    public interface ITypedMessageTypedMethodMyService
    {
        [OperationContract(Action = "http://FooMessageRequestAAction")]
        void MyOperation(FooMessage1 request);

        [OperationContract]
        void Method1(byte b);
    }

    [MessageContract]
    public class FooMessage1
    {
#if NET472
        [MessageHeader(Actor = "IDRequestActor", MustUnderstand = true, Name = "XmlID", Namespace = "XmlIDNamespace", Relay = true)]
#else
        [MessageHeader( MustUnderstand = true, Name = "XmlID", Namespace = "XmlIDNamespace")]
#endif
        [XmlElement(ElementName = "XmlID", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "XmlIDNamespace")]
        public int ID;
#if NET472
        [MessageHeader(Actor = "FooRequestActor", MustUnderstand = true, Name = "MessageFoo", Namespace = "MessageFooNamespace", Relay = false)]
#else
        [MessageHeader(MustUnderstand = true, Name = "MessageFoo", Namespace = "MessageFooNamespace")]
#endif
        public Foo foo;

        [MessageBodyMember(Name = "MessageRequest", Namespace = "", Order = 2)]
        [XmlElement]
        public string request;
    }

    [XmlType(TypeName = "XmlFoo", Namespace = "http://XmlFooNs/")]
    [SoapType(TypeName = "SoapFoo", Namespace = "http://SoapFooNs/")]
    [DataContract(Name = "DCFoo", Namespace = "http://DCFoo")]
    [XmlRoot(ElementName = "XmlRootFoo", Namespace = "http://XmlRootFoo", IsNullable = true)]
    public class Foo
    {
        [XmlElement(ElementName = "XmlFooName")]
        [SoapElement(ElementName = "SoapFooName")]
        [DataMember]
        public string FooName;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService2
    {
        [OperationContract(IsOneWay = true, Action = "FooMessage2")]
        void MyOperation(FooMessage2 request);

        [OperationContract]
        void Method2();
    }

    [MessageContract]
    public class FooMessage2
    {
        [MessageHeader]
        public int Name;

        [MessageBodyMember]
        [XmlArray(ElementName = "FooArray", Namespace = "http://FooNamespace", IsNullable = true)]
        [XmlArrayItem(ElementName = "FooA")]
        public Foo[] foos;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService3
    {
        [OperationContract(IsOneWay = true, Action = "*")]
        void MyOperation(FooMessage3 request);

        [OperationContract]
        int Method3(float num);
    }

    [MessageContract]
    public class FooMessage3
    {
        [MessageBodyMember]
        public int Name;

        [MessageHeader(Name = "FooArray", Namespace = "http://FooNamespace")]
        [XmlArray(ElementName = "FooArray", Namespace = "http://FooNamespace", IsNullable = true)]
        [XmlArrayItem(ElementName = "FooA")]
        public Foo[] foos;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService4
    {
        [OperationContract(IsOneWay = true)]
        void MyOperation(FooMessage4 request);

        [OperationContract]
        bool Method4(double dblnum, decimal decnum);
    }

    [MessageContract()]
    public class FooMessage4
    {
        //        [MessageHeader(Name = "MyID")]
        [MessageHeader(Name = "XmlID")]
        [XmlElement(ElementName = "XmlID")]
        public int ID;

        //        [MessageHeader(Name = "MyNewID")]
        [MessageHeader(Name = "XmlNewID")]
        [XmlElement(ElementName = "XmlNewID")]
        public int newID;

        [MessageBodyMember(Name = "MyName")]
        [XmlElement(ElementName = "XmlName")]
        public string Name;

        [MessageBodyMember(Name = "MyAddress")]
        [XmlElement(ElementName = "XmlAddress")]
        public string Address;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService5
    {
        [OperationContract(IsOneWay = true, Action = "")]
        void MyOperation(FooMessage5 request);

        [OperationContract]
        char Method5(string str, byte[] arrbyte);
    }

    [MessageContract]
    public class FooMessage5
    {
        [MessageHeader]
        public int Name;

        [MessageBodyMember]
        [XmlArray(ElementName = "FooArray", Namespace = "http://FooNamespace", IsNullable = true)]
        [XmlArrayItem(ElementName = "FooA")]
        public Foo[] foos;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService6
    {
        [OperationContract(Action = "http://FooMessage6Action")]
        void MyOperation(FooMessage6 request);

        [OperationContract]
        DateTime Method6(int num, Foo foo);
    }

    [MessageContract]
    public class FooMessage6
    {
        // [MessageProperty(Name = "Property1")] MessageProperty don't support
        [XmlElement(ElementName = "XmlID", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "XmlIDNamespace")]
        public int ID;

        //[MessageProperty(Name = "Property2")]
        [XmlAnyElement(Name = "XmlAnyElementName", Namespace = "XmlAnyElementNamespace", Order = 2)]
        public XmlElement[] elements1;

        // [MessageProperty(Name = "Property3")]
        [XmlArray(ElementName = "XmlArrayElementName", Namespace = "XmlArrayNamespace", Order = 2)]
        public XmlElement[] elements2;
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService7
    {
        [OperationContract]
        void MyOperation(Person request);

        [OperationContract]
        [ServiceKnownType(typeof(Person))]
        Foo[] Method7(string str, ArrayList list);
    }

    [MessageContract()]
    public class Person
    {
        //[MessageBodyMember(Namespace = "http://tempuri.org/Imports")]
        [MessageBodyMember()]
        public string address = "One Microsoft Way";

        //[MessageHeader(Namespace = "http://tempuri.org/Imports")]
        [MessageHeader]
        public string name = "Indigo";
    }

    [ServiceContract()]
    [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
    interface ITypedMessageTypedMethodMyService8
    {
        [OperationContract]
        void MyOperation(Manager request);

        [OperationContract]
        decimal Method8(string str, bool b);
    }

    [MessageContract()]
    public class Manager
    {
        [MessageBodyMember()]
        public Address Address = new Address();

        [MessageHeader()]
        public string name = "Indigo";
    }

    [DataContract(Namespace = "")]
    public class Address
    {
        [DataMember]
        public string address = "One Microsoft Way";
    }
}
