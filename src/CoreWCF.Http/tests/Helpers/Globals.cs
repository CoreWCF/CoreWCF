using System;
using System.Runtime.Serialization;
using System.Xml.Schema;

namespace Helpers
{
    internal static class Globals
    {
        internal static Type TypeOfObject = typeof(object);
        internal static Type TypeOfValueType = typeof(ValueType);
        internal static Type TypeOfString = typeof(string);
        internal static Type TypeOfInt = typeof(int);
        internal static Type TypeOfLong = typeof(long);
        internal static Type TypeOfULong = typeof(ulong);
        internal static Type TypeOfVoid = typeof(void);
        internal static Type TypeOfDouble = typeof(double);
        internal static Type TypeOfByte = typeof(byte);
        internal static Type TypeOfByteArray = typeof(byte[]);
        internal static Type TypeOfIntPtr = typeof(IntPtr);
        internal static Type TypeOfStreamingContext = typeof(StreamingContext);
        //internal static unsafe Type TypeOfBytePtr = typeof(byte*);
        internal static Type TypeOfDataContractAttribute = typeof(DataContractAttribute);
        internal static Type TypeOfDataMemberAttribute = typeof(DataMemberAttribute);
        internal static Type TypeOfObjectArray = typeof(object[]);
        internal static Type TypeOfOnSerializingAttribute = typeof(OnSerializingAttribute);
        internal static Type TypeOfOnSerializedAttribute = typeof(OnSerializedAttribute);
        internal static Type TypeOfOnDeserializingAttribute = typeof(OnDeserializingAttribute);
        internal static Type TypeOfOnDeserializedAttribute = typeof(OnDeserializedAttribute);
        internal static Type TypeOfFlagsAttribute = typeof(FlagsAttribute);

        internal static string Space = " ";
        internal static string SchemaInstanceNamespace = XmlSchema.InstanceNamespace;
        internal static string SchemaNamespace = XmlSchema.Namespace;
        internal static string XsiPrefix = "xsi";
        internal static string SerPrefix = "ser";
        internal static string DefaultNamespace = "http://tempuri.org/";
        internal static string XsiNilLocalName = "nil";
        internal static string XsiTypeLocalName = "type";
        internal static string TnsPrefix = "tns";
        internal static string OccursUnbounded = "unbounded";
        internal static string AnyTypeLocalName = "anyType";

        internal static string DefaultClrNamespace = "GeneratedNamespace";
        internal static string DefaultTypeName = "GeneratedType";
        internal static string DefaultGeneratedMember = "GeneratedMember";
        internal static string DefaultFieldSuffix = "Field";
        internal static string DefaultMemberSuffix = "Member";
        internal static string DefaultEnumMemberName = "Value";
        internal static string NameProperty = "Name";
        internal static string VersionAddedProperty = "VersionAdded";
        internal static string ClrNamespaceProperty = "ClrNamespace";
        internal static string DataContractNamespaceProperty = "DataContractNamespace";
        internal static int DefaultVersion = 1;

        // NOTE: These values are used in schema below. If you modify any value, please make the same change in the schema.
        internal static string SerializationNamespace = "http://schemas.microsoft.com/2003/10/Serialization/";
        internal static string ClrTypeLocalName = "ClrType";
        internal static string ClrAssemblyLocalName = "ClrAssembly";
        internal static string BaseTypesLocalName = "BaseTypes";
        internal static string TypeDelimiterLocalName = "TypeDelimiter";
        internal static string VersionDelimiterLocalName = "VersionDelimiter";
        internal static string TypeAttributesLocalName = "TypeAttributes";
        internal static string BaseArrayLocalName = "Array";
        internal static string BaseArrayNamespace = SerializationNamespace;
        internal static string ArrayItemLocalName = "Item";
        internal static string ArrayItemNamespace = String.Empty;
        internal static string ArrayDimensionsLocalName = "Dimensions";
        internal static string ArrayItemTypeLocalName = "ItemType";
        internal static string EnumerationMemberNameLocalName = "EnumerationMemberName";
        internal static string EnumerationIsFlagsLocalName = "EnumerationIsFlags";

        internal static string SerializationSchema =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsd:schema elementFormDefault=""qualified"" attributeFormDefault=""qualified"" xmlns:tns=""http://schemas.microsoft.com/2003/10/Serialization/"" targetNamespace=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">

  <!-- Attributes common to serialized instances -->
  <xsd:attributeGroup name=""TypeAttributes"">
    <xsd:attribute name=""Id"" type=""xsd:string"" />
    <xsd:attribute name=""Ref"" type=""xsd:string"" />
    <xsd:attribute name=""BaseTypes"">
      <xsd:simpleType>
        <xsd:list itemType=""xsd:QName"" />
      </xsd:simpleType>
    </xsd:attribute>
    <xsd:attribute name=""ClrType"" type=""xsd:string"" />
    <xsd:attribute name=""ClrAssembly"" type=""xsd:string"" />
  </xsd:attributeGroup>
  
  <!-- Delimiters -->
  <xsd:element name=""TypeDelimiter"">
    <xsd:complexType />
  </xsd:element>
  <xsd:element name=""VersionDelimiter"">
    <xsd:complexType>
      <xsd:attribute name=""Version"" type=""xsd:int"" />
    </xsd:complexType>
  </xsd:element>

  <!-- System.Char -->
  <xsd:simpleType name=""char"">
    <xsd:restriction base=""xsd:string"">
        <xsd:length value=""1"" />
    </xsd:restriction>
  </xsd:simpleType>

  <!-- Global elements for primitive types -->
  <xsd:element name=""anyType"" nillable=""true"" type=""xsd:anyType"" />
  <xsd:element name=""base64Binary"" nillable=""true"" type=""xsd:base64Binary"" />
  <xsd:element name=""boolean"" nillable=""true"" type=""xsd:boolean"" />
  <xsd:element name=""byte"" nillable=""true"" type=""xsd:byte"" />
  <xsd:element name=""char"" nillable=""true"" type=""tns:char"" />
  <xsd:element name=""dateTime"" nillable=""true"" type=""xsd:dateTime"" />
  <xsd:element name=""decimal"" nillable=""true"" type=""xsd:decimal"" />
  <xsd:element name=""double"" nillable=""true"" type=""xsd:double"" />
  <xsd:element name=""float"" nillable=""true"" type=""xsd:float"" />
  <xsd:element name=""int"" nillable=""true"" type=""xsd:int"" />
  <xsd:element name=""long"" nillable=""true"" type=""xsd:long"" />
  <xsd:element name=""short"" nillable=""true"" type=""xsd:short"" />
  <xsd:element name=""string"" nillable=""true"" type=""xsd:string"" />
  <xsd:element name=""unsignedByte"" nillable=""true"" type=""xsd:unsignedByte"" />
  <xsd:element name=""unsignedInt"" nillable=""true"" type=""xsd:unsignedInt"" />
  <xsd:element name=""unsignedLong"" nillable=""true"" type=""xsd:unsignedLong"" />
  <xsd:element name=""unsignedShort"" nillable=""true"" type=""xsd:unsignedShort"" />

  <!-- Arrays at top level -->
  <xsd:element name=""Array"" type=""tns:Array"" />
  <xsd:complexType name=""Array"">
    <xsd:sequence minOccurs=""0"">
      <xsd:element name=""Item"" type=""xsd:anyType"" minOccurs=""0"" maxOccurs=""unbounded"" nillable=""true"" form=""unqualified"" />
    </xsd:sequence>
    <xsd:attribute name=""ItemType"" type=""xsd:QName"" default=""xsd:anyType"" />
    <xsd:attribute name=""Dimensions"" default=""1"">
      <xsd:simpleType>
        <xsd:list itemType=""xsd:int"" />
      </xsd:simpleType>
    </xsd:attribute>
    <xsd:attribute default=""0"" name=""LowerBounds"">
      <xsd:simpleType>
        <xsd:list itemType=""xsd:int"" />
      </xsd:simpleType>
    </xsd:attribute>
    <xsd:attributeGroup ref=""tns:TypeAttributes"" />
  </xsd:complexType>

  <xsd:element name=""EnumerationMemberName"" type=""xsd:string"" /> 
  <xsd:element name=""EnumerationIsFlags"" type=""xsd:boolean"" /> 

  <!-- ISerializable -->
  <xsd:complexType name=""ObjectData"">
    <xsd:complexContent>
      <xsd:restriction base=""tns:Array"">
        <xsd:sequence minOccurs=""0"">
          <xsd:element name=""Item"" type=""tns:ObjectDataItem"" minOccurs=""0"" maxOccurs=""unbounded"" nillable=""true"" form=""unqualified""/>
        </xsd:sequence>
      </xsd:restriction>
    </xsd:complexContent>
  </xsd:complexType>
  <xsd:complexType name=""ObjectDataItem"">
    <xsd:sequence minOccurs=""0"">
      <xsd:element name=""Key"" type=""xsd:anyType"" minOccurs=""1"" maxOccurs=""1"" nillable=""true"" />
      <xsd:element name=""Value"" type=""xsd:anyType"" minOccurs=""1"" maxOccurs=""1"" nillable=""true"" />
    </xsd:sequence>
  </xsd:complexType>

  <!-- Complex types to allow boxed primitives -->
  <xsd:complexType name=""base64BinaryRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:base64Binary"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""booleanRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:boolean"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""byteRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:byte"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""charRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""tns:char"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""dateTimeRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:dateTime"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""decimalRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:decimal"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""doubleRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:double"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""floatRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:float"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""intRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:int"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""longRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:long"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""shortRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:short"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""stringRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:string"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""unsignedByteRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:unsignedByte"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""unsignedIntRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:unsignedInt"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""unsignedLongRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:unsignedLong"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
  <xsd:complexType name=""unsignedShortRefType"">
    <xsd:simpleContent>
      <xsd:extension base=""xsd:unsignedShort"">
        <xsd:attributeGroup ref=""tns:TypeAttributes"" />
      </xsd:extension>
    </xsd:simpleContent>
  </xsd:complexType>
</xsd:schema>
";
    }
}