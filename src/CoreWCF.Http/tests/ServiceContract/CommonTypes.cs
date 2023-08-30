// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Helpers;

namespace ServiceContract
{
    public class IReadWriteXmlWriteAttributesFromReader : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteAttributes") == false)
            {
                throw new MyException("Could not find the end of WriteAttributes");
            }

            if (reader.HasAttributes)
            {
                int attribCount = 0;
                while (reader.MoveToNextAttribute())
                {
                    attribCount++;
                }

                if (attribCount != 3)
                {
                    throw new MyException(String.Format("XmlReader says this node has no elements {0} {1} and I expect 3", reader.Name, reader.NodeType));
                }
            }
            else
            {
                throw new MyException(String.Format("XmlReader says this node has no elements {0} {1} and I expect 3", reader.Name, reader.NodeType));
            }
            reader.MoveToElement(); // back at WriteAttributes
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteAttributes");
            using (StringReader sr = new StringReader(CommonUtilities.XmlStringForAttributes))
            {
                using (XmlReader reader = new XmlTextReader(sr))
                {
                    bool found = false;
                    while (reader.Read())
                    {
                        if (reader.Name.Equals("attributeHolder"))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == false) throw new Exception("could not find the attributeHolder node");
                    // reader.ReadElementString("attributeHolder"); //moves to the root node
                    writer.WriteAttributes(reader, false);
                }
            }
            writer.WriteEndElement();
        }
    }

    public class IReadWriteXmlWriteAttributesString : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteAttributeString") == false)
            {
                throw new MyException("Could not find the end of WriteAttributeString");
            }

            if (reader.HasAttributes)
            {
                int attribCount = 0;
                string msg = null;
                while (reader.MoveToNextAttribute())
                {
                    Trace.WriteLine(String.Format("attribute[{0}], localname=\"{1}\" value=\"{2}\" namespaceURI=\"{3}\"", attribCount, reader.LocalName, reader.Value, reader.NamespaceURI));
                    if (String.IsNullOrEmpty(reader.LocalName)) continue;
                    switch (reader.LocalName)
                    {
                        case "attributeName":
                            if (reader.Prefix == null || !reader.Prefix.Equals("abc"))
                            {
                                msg = String.Format("attributeName: reader.Name = {0}, reader.NamespaceURI = {1} reader.Value = {2} reader.Prefix = {3}", reader.Name, reader.NamespaceURI, reader.Value, reader.Prefix);
                                throw new MyException(msg);
                            }
                            break;
                        case "attributeName2":
                            break;
                        case "attributeName3":
                            if (reader.NamespaceURI == null || !reader.NamespaceURI.Equals("myNameSpace3"))
                            {
                                msg = String.Format("attributeName: reader.Name = {0}, reader.NamespaceURI = {1} reader.ReadAttributeValue() = {2} reader.Prefix = {3}", reader.Name, reader.NamespaceURI, reader.Value, reader.Prefix);
                                throw new MyException(msg);
                            }
                            break;
                        default:

                            if (reader.Value != null && reader.Value.Equals("myNameSpace3"))
                            {
                                // this is my namespace declaration, it is OK
                                break;
                            }
                            if (reader.LocalName != null && reader.LocalName.Equals("abc"))
                            {
                                // this is my namespace declaration, it is OK
                                break;
                            }
                            msg = String.Format("DEFAULT LocalName <{0}> \r\n reader.Name = <{1}>, \r\n reader.NamespaceURI = <{2}> \r\n reader.ReadAttributeValue() = {3}", reader.LocalName, reader.Name, reader.NamespaceURI, reader.Value);
                            throw new MyException(msg);
                    }
                    attribCount++;
                }

                if (attribCount != 5)
                {
                    throw new MyException(String.Format("XmlReader says this node {0} {1} has no elements  and I expect 4", reader.Name, reader.NodeType));
                }
            }
            else
            {
                throw new MyException(String.Format("XmlReader says this node has no elements {0} {1} and I expect 3", reader.Name, reader.NodeType));
            }
            reader.MoveToElement(); // back at WriteAttributes
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteAttributeString");
            writer.WriteAttributeString("abc", "attributeName", "myNameSpace", "attributeValue");
            writer.WriteAttributeString("attributeName2", "attributeValue2");
            writer.WriteAttributeString("attributeName3", "myNameSpace3", "attributeValue3");
            writer.WriteEndElement(); // WriteAttributeString
        }
    }

    public class IReadWriteXmlWriteBase64 : IXmlSerializable
    {
        private string msg = null;
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteBase64") == false)
            {
                throw new MyException("Could not find the end of WriteBase64");
            }
            reader.Read();
            string base64 = reader.ReadContentAsString();
            byte[] bytes = Convert.FromBase64String(base64);
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            if (text == null || text.Equals("hello world") == false)
            {
                msg = String.Format("WriteBase64: reader.Name = {0}, reader.NamespaceURI = {1} reader.Value = {2} base64 = {3}", reader.Name, reader.NamespaceURI, reader.Value, base64);
                throw new MyException(msg);
            }

            reader.MoveToElement();  // move back to WriteBase64 end

            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteBase64");
            byte[] bits = System.Text.Encoding.UTF8.GetBytes("hello world");
            writer.WriteBase64(bits, 0, bits.Length);
            writer.WriteEndElement(); // WriteBase64
        }
    }

    public class IReadWriteXmlWriteBinHex : IXmlSerializable
    {
        private string msg = null;
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteBinHex") == false)
            {
                throw new MyException("Could not find the end of WriteBinHex");
            }
            reader.Read();
            string binHexString = reader.ReadContentAsString(); // good enuff.
            if (binHexString == null || binHexString.Length == 0)
            {
                msg = String.Format("WriteBinHex: reader.Name = {0}, reader.NamespaceURI = {1} reader.Value = {2} binHexString = {3}", reader.Name, reader.NamespaceURI, reader.Value, binHexString);
                throw new MyException(msg);
            }

            reader.MoveToElement();  // move back to WriteBinHex end
            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            byte[] bits = System.Text.Encoding.UTF8.GetBytes("hello world");
            writer.WriteStartElement("WriteBinHex");
            writer.WriteBinHex(bits, 0, bits.Length); //writes hello world again
            writer.WriteEndElement(); // WriteBinHex
        }
    }

    public class IReadWriteXmlWriteCData : IXmlSerializable
    {
        private string msg = null;
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteCData") == false)
            {
                throw new MyException("Could not find the start of WriteCData");
            }
            string cdata = reader.ReadString();
            if (cdata == null || cdata.Equals("<hello world/>") == false)
            {
                msg = String.Format("WriteCData: reader.Name = {0}, reader.NamespaceURI = {1} reader.Value = {2} cdata = {3}", reader.Name, reader.NamespaceURI, reader.Value, cdata);
                throw new MyException(msg);
            }
            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }
            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteCData");
            writer.WriteCData("<hello world/>");
            writer.WriteEndElement(); // WriteCData
        }
    }

    public class IReadWriteXmlWriteComment : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteComment") == false)
            {
                throw new MyException("Could not find the start of WriteComment");
            }

            int counter = 0;
            const int NODES_AT_END = 3;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteComment");
            writer.WriteComment("<hello world/>");
            writer.WriteEndElement(); // WriteComment
        }
    }

    public class IReadWriteXmlWriteContainerElement : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("ContainerElement") == false)
            {
                throw new MyException("could not find the start of element ContainerElement");
            }

            int depth = reader.Depth;
            reader.ReadToDescendant("xx1:containee");
            reader.ReadToNextSibling("xx2", "Containee2");
            reader.ReadToNextSibling("Containee3");

            // moved to closing ContainerElement
            while (reader.Depth > depth && reader.Read()) { }
            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }
            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("ContainerElement");
            writer.WriteElementString("xx1", "containee", "ContainedNS", "containedelementvalue");
            writer.WriteElementString("xx2", "Containee2", "containedelementvalue2");
            writer.WriteElementString("Containee3", "containedelementvalue3");
            writer.WriteEndElement(); // ContainerElement
        }
    }

    public class IReadWriteXmlWriteFullEndElement : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteFullEndElement") == false)
            {
                throw new MyException("could not find the start of element WriteFullEndElement");
            }

            int counter = 0;
            const int NODES_AT_END = 2;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteFullEndElement");
            writer.WriteFullEndElement(); //WriteFullEndElement
        }
    }

    public class IReadWriteXmlWriteName : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteName") == false)
            {
                throw new MyException("could not find the start of element WriteName");
            }

            int counter = 0;
            const int NODES_AT_END = 3;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("WriteName");
            writer.WriteName("spomeName");
            writer.WriteEndElement(); //WriteName
        }
    }

    public class IReadWriteXmlWriteChars : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            string msg = null;

            reader.Read();
            string value = reader.ReadContentAsString();
            if (value == null || value.Length != CommonUtilities.XmlCharacterArray.Length)
            {
                msg = "bad write data in WriteChars ";
                if (value == null)
                    msg += " value == null";
                else
                    msg += String.Format("original size {0} output size {1}", value.Length, CommonUtilities.XmlCharacterArray.Length);
                throw new MyException(msg);
            }

            int errorCount = 0;
            string firstMsg = null;
            for (int i = 0; i < CommonUtilities.XmlCharacterArray.Length; i++)
            {
                char originalChar = CommonUtilities.XmlCharacterArray[i];
                if (CommonUtilities.XmlCharacterArray[i] != value[i])
                {
                    // special case: XML reader/writer might normalize \r or \r\n to \n (http://www.w3.org/TR/REC-xml#sec-line-ends)
                    if (value[i] == '\n' && CommonUtilities.XmlCharacterArray[i] == '\r')
                    {
                        // do nothing
                    }
                    else
                    {
                        errorCount++;
                        if (firstMsg == null)
                        {
                            firstMsg = String.Format("Error in string, location = {0}, originalCharacter code = {1}, return character code {2}",
                            i, (int)CommonUtilities.XmlCharacterArray[i], (int)value[i]);
                        }
                    }
                }
            }

            if (errorCount > 0)
            {
                throw new MyException(firstMsg);
            }

            int counter = 0;
            const int NODES_AT_END = 0;
            while (reader.Read())
            {
                counter++;
            }
            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteChars(CommonUtilities.XmlCharacterArray, 0, CommonUtilities.XmlCharacterArray.Length);
        }
    }

    public class IReadWriteXmlWriteStartAttribute : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("WriteStartAttribute") == false)
            {
                throw new Exception("could not find the start of element WriteStartAttribute");
            }

            if (reader.HasAttributes)
            {
                int attribCount = 0;
                while (reader.MoveToNextAttribute())
                {
                    attribCount++;
                }
                if (attribCount != 2)
                {
                    throw new MyException(String.Format("expected 2 attributes, but found {0}", attribCount));
                }
            }
            else
            {
                throw new MyException(String.Format("XmlReader says this node has no elements {0} {1} and I expect 1", reader.Name, reader.NodeType));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {

            writer.WriteStartElement("WriteStartAttribute");
            writer.WriteStartAttribute("wsa", "WriteStartAttribute", "WriteStartAttributeNS");
            // bug 18930
            writer.WriteEndAttribute();
            writer.WriteEndElement(); //WriteStartAttribute
        }
    }

    public class IReadWriteXmlNestedWriteString : IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("NestedWriteString") == false)
            {
                throw new Exception("could not find the start of element NestedWriteString");
            }
            int depth = reader.Depth;
            reader.ReadToDescendant("container2");
            string content = reader.ReadString();
            if (content == null || content.Equals("container2 content") == false)
            {
                string msg = String.Format("NextWriteString: reader.Name = {0}, reader.NamespaceURI = {1} reader.Value = {2} content = {3}", reader.Name, reader.NamespaceURI, reader.Value, content);
                throw new Exception(msg);
            }
            // moved to closing NestedWriteString
            while (reader.Depth > depth && reader.Read()) { }

            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }
            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("NestedWriteString");
            writer.WriteStartElement("container2");
            writer.WriteString("container2 content");
            writer.WriteEndElement(); //container2
            writer.WriteEndElement(); //NestedWriteString
        }
    }

    public class IReadWriteXmlLotsOfData : IXmlSerializable
    {
        private static readonly DateTime Now = new DateTime(2005, 03, 11, 10, 54, 23, 456);
        private const string anotherString = "Yet another string << /> /> -->";

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new NotImplementedException();
        }

        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.ReadToDescendant("LotsOfData") == false)
            {
                throw new Exception("could not find the start of element LotsOfData");
            }

            if (reader.ReadToDescendant("Boolean") == false)
            {
                throw new Exception("could not find the start of element Boolean");
            }

            reader.Read();
            if (reader.ReadContentAsBoolean() == false)
            {
                throw new Exception("did not find the correct value in Boolean");
            }

            //DateTime
            if (reader.ReadToNextSibling("DateTime") == false)
            {
                throw new Exception("could not find the start of element DateTime");
            }

            reader.Read();
            DateTime now = reader.ReadContentAsDateTime();

            if (now != Now)
            {
                TimeSpan diff = new TimeSpan((now.Ticks - Now.Ticks));
                if (diff.TotalMilliseconds > 1000)
                {
                    // seconds are lost in Xml
                    throw new Exception(String.Format("Dates differ {0} {1} Ticks {2}", now, Now, (now.Ticks - Now.Ticks)));
                }
            }

            //DecimalValue
            if (reader.ReadToNextSibling("DecimalValue") == false)
            {
                throw new Exception("could not find the start of element DecimalValue");
            }
            //			reader.Read();
            decimal decimalValue = (decimal)reader.ReadElementContentAs(typeof(decimal), null);
            if (decimalValue != CommonUtilities.DecimalValue)
            {
                string msg = String.Format("different decimal Values {0} {1}", decimalValue, CommonUtilities.DecimalValue);
                throw new Exception(msg);
            }

            //DoubleValue
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "DoubleValue")
            {
                if (reader.ReadToNextSibling("DoubleValue") == false)
                {
                    throw new Exception("could not find the start of element DoubleValue");
                }
            }

            //reader.Read();
            double doubleValue = (double)reader.ReadElementContentAsDouble();
            if (doubleValue != CommonUtilities.DoubleValue)
            {
                string msg = String.Format("different double Values {0} {1}", doubleValue, CommonUtilities.DoubleValue);
                throw new Exception(msg);
            }

            //FloatValue
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "FloatValue")
            {
                if (reader.ReadToNextSibling("FloatValue") == false)
                {
                    throw new Exception("could not find the start of element FloatValue");
                }
            }

            //reader.Read();
            float floatValue = (float)reader.ReadElementContentAs(typeof(float), null);
            if (floatValue != CommonUtilities.FloatValue)
            {
                string msg = String.Format("different floatValue Values {0} {1}", floatValue, CommonUtilities.FloatValue);
                throw new MyException(msg);
            }

            //IntValue
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "IntValue")
            {
                if (reader.ReadToNextSibling("IntValue") == false)
                {
                    throw new Exception("could not find the start of element IntValue");
                }
            }
            //			reader.Read();
            int intValue = reader.ReadElementContentAsInt();
            if (intValue != CommonUtilities.IntValue)
            {
                string msg = String.Format("different intValue Values {0} {1}", intValue, CommonUtilities.IntValue);
                throw new MyException(msg);
            }

            //LongValue
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "LongValue")
            {
                if (reader.ReadToNextSibling("LongValue") == false)
                {
                    throw new Exception("could not find the start of element LongValue");
                }
            }
            //reader.Read();
            long longValue = (long)reader.ReadElementContentAs(typeof(long), null);
            if (longValue != CommonUtilities.LongValue)
            {
                string msg = String.Format("different longValue Values {0} {1}", longValue, CommonUtilities.LongValue);
                throw new MyException(msg);
            }

            //Object
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "Object")
            {
                if (reader.ReadToNextSibling("Object") == false)
                {
                    throw new MyException("could not find the start of element Object");
                }
            }

            //reader.Read();
            TimeSpan objectValue = (TimeSpan)reader.ReadElementContentAs(typeof(TimeSpan), null);
            if (objectValue != CommonUtilities.TimeSpanValue)
            {
                string msg = String.Format("different objectValue Values {0} {1}", objectValue, CommonUtilities.TimeSpanValue);
                throw new MyException(msg);
            }

            //StringValue
            if (reader.NodeType != System.Xml.XmlNodeType.Element && reader.Name != "StringValue")
            {
                if (reader.ReadToNextSibling("StringValue") == false)
                {
                    throw new MyException("could not find the start of element StringValue");
                }
            }

            //reader.Read();
            string stringValue = reader.ReadElementContentAsString();
            if (stringValue == null || stringValue.Equals(CommonUtilities.XmlStringForAttributes) == false)
            {
                string msg = String.Format("different stringValue Values {0} {1}", stringValue, CommonUtilities.XmlStringForAttributes);
                throw new MyException(msg);
            }

            int counter = 0;
            const int NODES_AT_END = 1;
            while (reader.Read())
            {
                counter++;
            }

            if (counter != NODES_AT_END)
            {
                throw new MyException(String.Format("expected {0} nodes, but found {1}", NODES_AT_END, counter));
            }
        }

        public virtual void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("LotsOfData");
            writer.WriteStartElement("Boolean");
            writer.WriteValue(true);
            writer.WriteEndElement(); //Boolean
            writer.WriteStartElement("DateTime");
            writer.WriteValue(Now);
            writer.WriteEndElement(); //DateTime
            writer.WriteStartElement("DecimalValue");
            writer.WriteValue(CommonUtilities.DecimalValue); //decimal
            writer.WriteEndElement(); //DecimalValue
            writer.WriteStartElement("DoubleValue");
            writer.WriteValue(CommonUtilities.DoubleValue); // double
            writer.WriteEndElement(); //DoubleValue
            writer.WriteStartElement("FloatValue");
            writer.WriteValue(CommonUtilities.FloatValue); //float
            writer.WriteEndElement(); //FloatValue
            writer.WriteStartElement("IntValue");
            writer.WriteValue(CommonUtilities.IntValue); //int
            writer.WriteEndElement(); //IntValue
            writer.WriteStartElement("LongValue");
            writer.WriteValue(CommonUtilities.LongValue); //long
            writer.WriteEndElement(); //LongValue
            writer.WriteStartElement("Object");
            writer.WriteValue(CommonUtilities.TimeSpanValue); //object
            writer.WriteEndElement(); //Object
            writer.WriteStartElement("StringValue");
            writer.WriteValue(CommonUtilities.XmlStringForAttributes); //string
            writer.WriteEndElement(); //StringValue
            writer.WriteEndElement(); //LotsOfData
        }
    }
}
