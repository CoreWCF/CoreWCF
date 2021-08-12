using System;

namespace ServiceContract
{
    using System.Runtime.Serialization;

    [DataContract(Namespace = "MyRelativePart")]
    public class TypeWithRelativeNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "http://www.b.com/7")]
    public class TypeWithNumberInNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "")]
    public class TypeWithEmptyNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "http://tempuri.org")]
    public class TypeWithDefaultNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "http://tempuri.org/llllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll")]
    public class TypeWithLongNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "http://tempuri.org/l\0llll\u4443lllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllllll")]
    public class TypeWithUnicodeInNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }

    [DataContract(Namespace = "public/class/")]
    public class TypeWithKeywordsInNamespace
    {
        [DataMember]
        public String data = "hello worls >";
    }
}