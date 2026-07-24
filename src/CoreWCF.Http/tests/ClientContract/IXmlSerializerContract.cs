using System;
using System.Collections;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ClientContract
{
	[XmlSchemaProvider("GetSchema")]
	[XmlRoot(ElementName = "PersonPerson", Namespace = "http://Test")]
	public class XmlSerializerPerson : IXmlSerializable
	{
		public void ReadXml(XmlReader xmlReader)
		{

		}

		public void WriteXml(XmlWriter writer)
		{

		}

		public XmlSchema GetSchema()
		{
			throw new NotImplementedException();
		}

		public static XmlQualifiedName GetSchema(XmlSchemaSet schemas)
		{
			XmlQualifiedName xmlQualifiedName = new XmlQualifiedName("Person", "http://Test");
			XmlTypeHelper.AddSchema(schemas, xmlQualifiedName);
			return xmlQualifiedName;
		}
	}

	[ServiceContract(Namespace = "http://microsoft.samples", Name = "IXmlSerializerContract")]
	public interface IXmlSerializerContract
	{
		[XmlSerializerFormat]
		[OperationContract]
		Task<XmlSerializerPerson> GetPerson();
	}

	public class XmlTypeHelper
	{
		private XmlTypeHelper()
		{

		}

		public static XmlSchema GetSchema(string localName, string ns)
		{
			XmlSchemaType xmlSchemaType = null;
			return XmlTypeHelper.GetSchema(null, localName, ns, out xmlSchemaType);
		}

		private static XmlSchema GetSchema(XmlSchemaSet schemas, string localName, string ns, out XmlSchemaType schemaType)
		{
			schemaType = XmlTypeHelper.GetSchemaType(localName, ns);
			XmlSchema xmlSchema = null;
			if (schemas != null)
			{
				ICollection collection = schemas.Schemas();
				foreach (object obj in collection)
				{
					XmlSchema xmlSchema2 = (XmlSchema)obj;
					if ((xmlSchema2.TargetNamespace == null && ns.Length == 0) || ns.Equals(xmlSchema2.TargetNamespace))
					{
						xmlSchema = xmlSchema2;
						break;
					}
				}
			}
			if (xmlSchema == null)
			{
				xmlSchema = new XmlSchema();
				xmlSchema.ElementFormDefault = XmlSchemaForm.Qualified;
				if (!string.IsNullOrEmpty(ns))
				{
					xmlSchema.TargetNamespace = ns;
				}
				if (ns.Length > 0)
				{
					xmlSchema.Namespaces.Add("tns", ns);
				}
			}
			xmlSchema.Items.Add(schemaType);
			xmlSchema.Id = "ID_" + localName;
			return xmlSchema;
		}

		private static XmlSchemaType GetSchemaType(string localName, string ns)
		{
			XmlSchemaComplexType xmlSchemaComplexType = new XmlSchemaComplexType();
			xmlSchemaComplexType.IsMixed = true;
			xmlSchemaComplexType.Name = localName;
			xmlSchemaComplexType.Particle = new XmlSchemaSequence();
			XmlSchemaAny xmlSchemaAny = new XmlSchemaAny();
			xmlSchemaAny.MinOccurs = 0m;
			xmlSchemaAny.MaxOccurs = decimal.MaxValue;
			xmlSchemaAny.ProcessContents = XmlSchemaContentProcessing.Skip;
			((XmlSchemaSequence)xmlSchemaComplexType.Particle).Items.Add(xmlSchemaAny);
			xmlSchemaComplexType.AnyAttribute = new XmlSchemaAnyAttribute();
			xmlSchemaComplexType.AnyAttribute.ProcessContents = XmlSchemaContentProcessing.Skip;
			return xmlSchemaComplexType;
		}

		private static XmlSchemaType AddDefaultSchema(XmlSchemaSet schemas, string localName, string ns)
		{
			XmlSchemaType result = null;
			XmlSchema schema = XmlTypeHelper.GetSchema(schemas, localName, ns, out result);
			schemas.Add(schema);
			return result;
		}

		public static XmlSchemaType AddSchema(XmlSchemaSet schemas, string localName, string ns)
		{
			return XmlTypeHelper.AddDefaultSchema(schemas, localName, ns);
		}

		public static void AddSchema(XmlSchemaSet schemas, XmlQualifiedName qName)
		{
			XmlTypeHelper.AddDefaultSchema(schemas, qName.Name, qName.Namespace);
		}

		public static XmlQualifiedName AddSchema(XmlSchemaSet schemas, Type t)
		{
			XmlQualifiedName xmlQualifiedName = new XmlQualifiedName(t.Name, t.Namespace);
			XmlTypeHelper.AddDefaultSchema(schemas, xmlQualifiedName.Name, xmlQualifiedName.Namespace);
			return xmlQualifiedName;
		}
	}
}

