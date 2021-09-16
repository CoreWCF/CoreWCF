// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal class EncryptionMethodElement
    {
        public string Algorithm { get; set; }

        public string Parameters { get; set; }

        public void ReadXml( XmlDictionaryReader reader )
        {
            if ( reader == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull( nameof(reader));
            }

            reader.MoveToContent();
            if ( !reader.IsStartElement( XmlEncryptionConstants.Elements.EncryptionMethod, XmlEncryptionConstants.Namespace ) )
            {
                return;
            }

            Algorithm = reader.GetAttribute( XmlEncryptionConstants.Attributes.Algorithm, null );

            if ( !reader.IsEmptyElement )
            {
                //
                // Trace unread missing element
                //

                string xml = reader.ReadOuterXml();
                //if ( DiagnosticUtility.ShouldTraceWarning )
                //{
                //    TraceUtility.TraceString( System.Diagnostics.TraceEventType.Warning, SR.GetString( SR.ID8024, reader.Name, reader.NamespaceURI, xml ) );
                //}
            }
            else
            {
                //
                // Read to the next element
                //
                reader.Read();
            }
        }

        public void WriteXml( XmlWriter writer )
        {
            writer.WriteStartElement( XmlEncryptionConstants.Prefix, XmlEncryptionConstants.Elements.EncryptionMethod, XmlEncryptionConstants.Namespace );

            writer.WriteAttributeString( XmlEncryptionConstants.Attributes.Algorithm, null, Algorithm );

            // <EncryptionMethod>

            writer.WriteEndElement(); 
        }

    }
}
