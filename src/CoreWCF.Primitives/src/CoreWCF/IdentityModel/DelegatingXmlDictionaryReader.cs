// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using System;

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Class wraps a given reader and delegates all XmlDictionaryReader calls 
    /// to the inner wrapped reader.
    /// </summary>
    public class DelegatingXmlDictionaryReader : XmlDictionaryReader
    {

        /// <summary>
        /// Initializes a new instance of <see cref="DelegatingXmlDictionaryWriter" />
        /// </summary>
        protected DelegatingXmlDictionaryReader()
        {
        }

        /// <summary>
        /// Initializes the Inner reader that this instance wraps.
        /// </summary>
        /// <param name="innerReader">XmlDictionaryReader to wrap.</param>
        protected void InitializeInnerReader(XmlDictionaryReader innerReader)
        {
            if (innerReader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerReader));
            }

            InnerReader = innerReader;
        }

        /// <summary>
        /// Gets the wrapped inner reader.
        /// </summary>
        protected XmlDictionaryReader InnerReader { get; private set; }

        /// <summary>
        /// Gets the value of the attribute with the specified index.
        /// </summary>
        /// <param name="i">index of the attribute.</param>
        /// <returns>Attribute value at the specified index.</returns>
        public override string this[int i]
        {
            get { return InnerReader[i]; }
        }

        /// <summary>
        /// Gets the value of the attribute with the specified System.Xml.XmlReader.Name.
        /// </summary>
        /// <param name="name">The qualified name of the attribute.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, 
        /// null is returned.</returns>
        public override string this[string name]
        {
            get { return InnerReader[name]; }
        }

        /// <summary>
        /// Gets the value of the attribute with the specified System.Xml.XmlReader.LocalName and 
        /// System.Xml.XmlReader.NamespaceURI from the wrapped reader.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="namespaceURI">The namespace URI of the attribute.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, 
        /// null is returned.</returns>
        public override string this[string name, string namespaceURI]
        {
            get { return InnerReader[name, namespaceURI]; }
        }

        /// <summary>
        /// Gets the number of Attributes at the current reader position.
        /// </summary>
        public override int AttributeCount
        {
            get { return InnerReader.AttributeCount; }
        }

        /// <summary>
        /// Gets the base Uri of the current node.
        /// </summary>
        public override string BaseURI
        {
            get { return InnerReader.BaseURI; }
        }

        /// <summary>
        /// Gets the Depth of the current node.
        /// </summary>
        public override int Depth
        {
            get { return InnerReader.Depth; }
        }

        /// <summary>
        /// Gets a value indicating if reader is positioned at the end of the stream.
        /// </summary>
        public override bool EOF
        {
            get { return InnerReader.EOF; }
        }

        /// <summary>
        /// Gets a value indicating if the current node can have a 
        /// System.Xml.XmlReader.Value.
        /// </summary>
        public override bool HasValue
        {
            get { return InnerReader.HasValue; }
        }

        /// <summary>
        /// Gets a value indicating if the current node is an attribute that
        /// was generated from the default value defined in the DTD or Schema.
        /// </summary>
        public override bool IsDefault
        {
            get { return InnerReader.IsDefault; }
        }

        /// <summary>
        /// Gets a value indicating if the current node.
        /// </summary>
        public override bool IsEmptyElement
        {
            get { return InnerReader.IsEmptyElement; }
        }

        /// <summary>
        /// Gets the local name of the current node.
        /// </summary>
        public override string LocalName
        {
            get { return InnerReader.LocalName; }
        }

        /// <summary>
        /// Gets the qualified name of the current node.
        /// </summary>
        public override string Name
        {
            get { return InnerReader.Name; }
        }

        /// <summary>
        /// Gets the namespace URI of the current node.
        /// </summary>
        public override string NamespaceURI
        {
            get { return InnerReader.NamespaceURI; }
        }

        /// <summary>
        /// Gets the System.Xml.XmlNameTable associated with this instance.
        /// </summary>
        public override XmlNameTable NameTable
        {
            get { return InnerReader.NameTable; }
        }

        /// <summary>
        /// Gets the type of the current node.
        /// </summary>
        public override XmlNodeType NodeType
        {
            get { return InnerReader.NodeType; }
        }

        /// <summary>
        /// Gets the prefix of the current node.
        /// </summary>
        public override string Prefix
        {
            get { return InnerReader.Prefix; }
        }

        /// <summary>
        /// Gets the quotation mark character used to enclose the attribute node. (" or ')
        /// </summary>
        public override char QuoteChar
        {
            get { return InnerReader.QuoteChar; }
        }

        /// <summary>
        /// Gets the System.Xml.ReadState of the reader. 
        /// </summary>
        public override ReadState ReadState
        {
            get { return InnerReader.ReadState; }
        }

        /// <summary>
        /// Gets the text value of the current node.
        /// </summary>
        public override string Value
        {
            get { return InnerReader.Value; }
        }

        /// <summary>
        /// Gets the Common Language Runtime (CLR) type of the curent node.
        /// </summary>
        public override Type ValueType
        {
            get { return InnerReader.ValueType; }
        }

        /// <summary>
        /// Gets the xml:lang scope.
        /// </summary>
        public override string XmlLang
        {
            get { return InnerReader.XmlLang; }
        }

        /// <summary>
        /// Gets the current xml:space scope. If no xml:space scope exists, this property 
        /// defaults to XmlSpace.None.
        /// </summary>
        public override XmlSpace XmlSpace
        {
            get { return InnerReader.XmlSpace; }
        }

        /// <summary>
        /// Closes the reader and changes the System.Xml.XmlReader.ReadState
        /// to Closed.
        /// </summary>
        public override void Close()
        {
            InnerReader.Close();
        }

        /// <summary>
        /// Gets the value of the attribute at the given index.
        /// </summary>
        /// <param name="i">The index of the attribute. The index is 0 based index.</param>
        /// <returns>The value of the attribute at the specified index.</returns>
        /// <remarks>The method does not move the reader position.</remarks>
        public override string GetAttribute(int i)
        {
            return InnerReader.GetAttribute(i);
        }

        /// <summary>
        /// Gets the value of the attribute with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the attribute.</param>
        /// <returns>The value of the attribute. If the attribute is not found null
        /// is returned.</returns>
        /// <remarks>The method does not move the reader position.</remarks>
        public override string GetAttribute(string name)
        {
            return InnerReader.GetAttribute(name);
        }

        /// <summary>
        /// Gets the value of the attribute with the given name and namespace Uri.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="namespaceURI">The namespace of the attribute.</param>
        /// <returns>The value of the attribute. If the attribute is not found
        /// null is returned.</returns>
        /// <remarks>The method does not move the reader.</remarks>
        public override string GetAttribute(string name, string namespaceURI)
        {
            return InnerReader.GetAttribute(name, namespaceURI);
        }

        /// <summary>
        /// Resolves a namespace prefix in the current element scope.
        /// </summary>
        /// <param name="prefix">Prefix whose namespace Uri to be resolved.</param>
        /// <returns>The namespace Uri to which the prefix matches or null if no matching
        /// prefix is found.</returns>
        public override string LookupNamespace(string prefix)
        {
            return InnerReader.LookupNamespace(prefix);
        }

        /// <summary>
        /// Moves to the attribute with the specified index.
        /// </summary>
        /// <param name="i">The index of the attribute.</param>
        public override void MoveToAttribute(int i)
        {
            InnerReader.MoveToAttribute(i);
        }

        /// <summary>
        /// Moves to the attribute with the given local name.
        /// </summary>
        /// <param name="name">The qualified name of the attribute.</param>
        /// <returns>true if the attribute is found; otherwise, false.</returns>
        public override bool MoveToAttribute(string name)
        {
            return InnerReader.MoveToAttribute(name);
        }

        /// <summary>
        /// Moves to the attribute with the specified System.Xml.XmlReader.LocalName and 
        /// System.Xml.XmlReader.NamespaceURI.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="ns">The namespace URI of the attribute.</param>
        /// <returns>true if the attribute is found; otherwise, false.</returns>
        public override bool MoveToAttribute(string name, string ns)
        {
            return InnerReader.MoveToAttribute(name, ns);
        }

        /// <summary>
        /// Moves to a node of type Element.
        /// </summary>
        /// <returns>true if the reader is positioned on an element else false</returns>
        public override bool MoveToElement()
        {
            return InnerReader.MoveToElement();
        }

        /// <summary>
        /// Moves to the first attribute.
        /// </summary>
        /// <returns>Returns true if the reader is positioned at a attribute else false.</returns>
        /// <remarks>When returning false the reader position will not be changed.</remarks>
        public override bool MoveToFirstAttribute()
        {
            return InnerReader.MoveToFirstAttribute();
        }

        /// <summary>
        /// Moves the reader to the next attribute.
        /// </summary>
        /// <returns>Returns true if the reader is positioned at an attribute else false.</returns>
        /// <remarks>When returning false the reader position will not be changed.</remarks>
        public override bool MoveToNextAttribute()
        {
            return InnerReader.MoveToNextAttribute();
        }

        /// <summary>
        /// Reads the next node from the stream.
        /// </summary>
        /// <returns>true if the next node was read successfully.</returns>
        public override bool Read()
        {
            return InnerReader.Read();
        }

        /// <summary>
        /// Parses the attribute value into one or more Text, EntityReference, or EndEntity nodes.
        /// </summary>
        /// <returns>true if there are nodes to return.false if the reader is not positioned on
        /// an attribute node when the initial call is made or if all the attribute values
        /// have been read.</returns>
        public override bool ReadAttributeValue()
        {
            return InnerReader.ReadAttributeValue();
        }

        /// <summary>
        /// Reads the content and returns the Base64 decoded binary bytes.
        /// </summary>
        /// <param name="buffer">The buffer into which to copy the resulting text. This value cannot be null.</param>
        /// <param name="index">The offset into the buffer where to start copying the result.</param>
        /// <param name="count">The maximum number of bytes to copy into the buffer.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        public override int ReadContentAsBase64(byte[] buffer, int index, int count)
        {
            return InnerReader.ReadContentAsBase64(buffer, index, count);
        }

        /// <summary>
        /// Reads the content and returns the BinHex decoded binary bytes.
        /// </summary>
        /// <param name="buffer">The buffer into which to copy the resulting text. This value cannot be null.</param>
        /// <param name="index">The offset into the buffer where to start copying the result.</param>
        /// <param name="count">The maximum number of bytes to copy into the buffer.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        public override int ReadContentAsBinHex(byte[] buffer, int index, int count)
        {
            return InnerReader.ReadContentAsBinHex(buffer, index, count);
        }

        /// <summary>
        /// Reads the content and returns the contained string.
        /// </summary>
        public override System.Xml.UniqueId ReadContentAsUniqueId()
        {
            return InnerReader.ReadContentAsUniqueId();
        }

        /// <summary>
        /// Reads large streams of text embedded in an XML document.
        /// </summary>
        /// <param name="buffer">The array of characters that serves as the buffer to which the text contents
        /// are written. This value cannot be null.</param>
        /// <param name="index">The offset within the buffer where the System.Xml.XmlReader can start to
        /// copy the results.</param>
        /// <param name="count">The maximum number of characters to copy into the buffer. The actual number
        /// of characters copied is returned from this method.</param>
        /// <returns>The number of characters read into the buffer. The value zero is returned
        /// when there is no more text content.</returns>
        public override int ReadValueChunk(char[] buffer, int index, int count)
        {
            return InnerReader.ReadValueChunk(buffer, index, count);
        }

        /// <summary>
        /// Resolves the entity reference for EntityReference nodes.
        /// </summary>
        public override void ResolveEntity()
        {
            InnerReader.ResolveEntity();
        }
    }
}
