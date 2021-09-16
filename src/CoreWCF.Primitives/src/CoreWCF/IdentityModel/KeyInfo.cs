// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel
{
    internal class KeyInfo
    {
        private readonly SecurityTokenSerializer _keyInfoSerializer;
        private SecurityKeyIdentifier _ski;

        public KeyInfo( SecurityTokenSerializer keyInfoSerializer )
        {
            _keyInfoSerializer = keyInfoSerializer;
            _ski = new SecurityKeyIdentifier();
        }

        public string RetrievalMethod { get; private set; }

        public SecurityKeyIdentifier KeyIdentifier
        {
            get { return _ski; }
            set
            {
                if ( value == null )
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _ski = value;
            }
        }

        public virtual void ReadXml(XmlDictionaryReader reader)
        {
            if ( reader == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            reader.MoveToContent();
            if ( reader.IsStartElement(CoreWCF.XD.XmlSignatureDictionary.KeyInfo.Value, CoreWCF.XD.XmlSignatureDictionary.Namespace.Value ) )
            {
                // <KeyInfo>
                reader.ReadStartElement();

                while ( reader.IsStartElement() )
                {
                    // <RetrievalMethod>
                    if ( reader.IsStartElement( XmlSignatureConstants.Elements.RetrievalMethod, CoreWCF.XD.XmlSignatureDictionary.Namespace.Value ) )
                    {
                        string method = reader.GetAttribute(CoreWCF.XD.XmlSignatureDictionary.URI.Value );
                        if ( !string.IsNullOrEmpty( method ) )
                        {
                            RetrievalMethod = method;
                        }
                        reader.Skip();
                    }
                    // check if internal serializer can handle clause
                    else if ( _keyInfoSerializer.CanReadKeyIdentifierClause( reader ) )
                    {
                        _ski.Add( _keyInfoSerializer.ReadKeyIdentifierClause( reader ) );
                    }
                    // trace we skipped over an element
                    else if ( reader.IsStartElement() )
                    {
                        string xml = reader.ReadOuterXml();

                        //if ( DiagnosticUtility.ShouldTraceWarning )
                        //{
                        //    TraceUtility.TraceString( System.Diagnostics.TraceEventType.Warning, SR.GetString( SR.ID8023, reader.Name, reader.NamespaceURI, xml ) );
                        //}
                    }
                    reader.MoveToContent();
                }

                reader.MoveToContent();
                reader.ReadEndElement();
            }
        }
    }
}
