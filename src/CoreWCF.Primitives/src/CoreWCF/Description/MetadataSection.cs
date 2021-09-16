// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Serialization;
using XsdNS = System.Xml.Schema;

namespace CoreWCF.Description
{
    [XmlRoot(ElementName = MetadataStrings.MetadataExchangeStrings.MetadataSection, Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
    public class MetadataSection
    {
        public MetadataSection()
            : this(null, null, null)
        {
        }

        public MetadataSection(string dialect, string identifier, object metadata)
        {
            Dialect = dialect;
            Identifier = identifier;
            Metadata = metadata;
        }

       // static public string ServiceDescriptionDialect { get { return System.Web.Services.Description.ServiceDescription.Namespace; } }
        static public string XmlSchemaDialect { get { return System.Xml.Schema.XmlSchema.Namespace; } }
        static public string PolicyDialect { get { return MetadataStrings.WSPolicy.NamespaceUri; } }
        static public string MetadataExchangeDialect { get { return MetadataStrings.MetadataExchangeStrings.Namespace; } }

        [XmlAnyAttribute]
        public Collection<XmlAttribute> Attributes { get; } = new Collection<XmlAttribute>();

        [XmlAttribute]
        public string Dialect { get; set; }

        [XmlAttribute]
        public string Identifier { get; set; }

        [XmlAnyElement]
        [XmlElement(MetadataStrings.XmlSchema.Schema, typeof(XsdNS.XmlSchema), Namespace = XsdNS.XmlSchema.Namespace)]
        //typeof(WsdlNS.ServiceDescription) produces an XmlSerializer which can't export / import the Extensions in the ServiceDescription.  
        //We use change this to typeof(string) and then fix the generated serializer to use the Read/Write 
        //methods provided by WsdlNS.ServiceDesciption which use a pregenerated serializer which can export / import the Extensions.
        // [XmlElement(MetadataStrings.ServiceDescription.Definitions, typeof(WsdlNS.ServiceDescription), Namespace = WsdlNS.ServiceDescription.Namespace)]
        [XmlElement(MetadataStrings.MetadataExchangeStrings.MetadataReference, typeof(MetadataReference), Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
        [XmlElement(MetadataStrings.MetadataExchangeStrings.Location, typeof(MetadataLocation), Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
        [XmlElement(MetadataStrings.MetadataExchangeStrings.Metadata, typeof(MetadataSet), Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
        public object Metadata { get; set; }

        internal string SourceUrl { get; set; }

        public static MetadataSection CreateFromPolicy(XmlElement policy, string identifier)
        {
            if (policy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(policy));
            }

            if (!IsPolicyElement(policy))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(policy),
 SR.Format(SR.SFxBadMetadataMustBePolicy, MetadataStrings.WSPolicy.NamespaceUri, MetadataStrings.WSPolicy.Elements.Policy, policy.NamespaceURI, policy.LocalName));
            }

            MetadataSection section = new MetadataSection();

            section.Dialect = policy.NamespaceURI;
            section.Identifier = identifier;
            section.Metadata = policy;

            return section;
        }

        public static MetadataSection CreateFromSchema(XsdNS.XmlSchema schema)
        {
            if (schema == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(schema));
            }

            MetadataSection section = new MetadataSection();

            section.Dialect = MetadataSection.XmlSchemaDialect;
            section.Identifier = schema.TargetNamespace;
            section.Metadata = schema;

            return section;
        }

        internal static bool IsPolicyElement(XmlElement policy)
        {
            return (policy.NamespaceURI == MetadataStrings.WSPolicy.NamespaceUri
                || policy.NamespaceURI == MetadataStrings.WSPolicy.NamespaceUri15)
                && policy.LocalName == MetadataStrings.WSPolicy.Elements.Policy;
        }

    }
}
