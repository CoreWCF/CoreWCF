using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ClientContract
{
	[AttributeUsage(AttributeTargets.All, Inherited = false)]
	internal sealed class __DynamicallyInvokableAttribute : Attribute
	{
	}

	public interface IXmlSerializable
	{
		/// <summary>This method is reserved and should not be used. When implementing the <see langword="IXmlSerializable" /> interface, you should return <see langword="null" /> (<see langword="Nothing" /> in Visual Basic) from this method, and instead, if specifying a custom schema is required, apply the <see cref="T:System.Xml.Serialization.XmlSchemaProviderAttribute" /> to the class.</summary>
		/// <returns>An <see cref="T:System.Xml.Schema.XmlSchema" /> that describes the XML representation of the object that is produced by the <see cref="M:System.Xml.Serialization.IXmlSerializable.WriteXml(System.Xml.XmlWriter)" /> method and consumed by the <see cref="M:System.Xml.Serialization.IXmlSerializable.ReadXml(System.Xml.XmlReader)" /> method.</returns>
		// Token: 0x0600171B RID: 5915
		[__DynamicallyInvokable]
		XmlSchema GetSchema();

		/// <summary>Generates an object from its XML representation.</summary>
		/// <param name="reader">The <see cref="T:System.Xml.XmlReader" /> stream from which the object is deserialized. </param>
		// Token: 0x0600171C RID: 5916
		[__DynamicallyInvokable]
		void ReadXml(XmlReader reader);

		/// <summary>Converts an object into its XML representation.</summary>
		/// <param name="writer">The <see cref="T:System.Xml.XmlWriter" /> stream to which the object is serialized. </param>
		// Token: 0x0600171D RID: 5917
		[__DynamicallyInvokable]
		void WriteXml(XmlWriter writer);
	}

	[XmlSchemaProvider("GetSchema")]
	[XmlRoot(ElementName = "PersonPerson", Namespace = "http://Test")]
	public class XmlSerializerPerson : IXmlSerializable
	{
		// Token: 0x060008CB RID: 2251 RVA: 0x00002A18 File Offset: 0x00000C18
		public void ReadXml(XmlReader xmlReader)
		{
		}

		// Token: 0x060008CC RID: 2252 RVA: 0x00002A18 File Offset: 0x00000C18
		public void WriteXml(XmlWriter writer)
		{
		}

		// Token: 0x060008CD RID: 2253 RVA: 0x0001C6F0 File Offset: 0x0001A8F0
		public XmlSchema GetSchema()
		{
			throw new NotImplementedException();
		}

		// Token: 0x060008CE RID: 2254 RVA: 0x00020804 File Offset: 0x0001EA04
		public static XmlQualifiedName GetSchema(XmlSchemaSet schemas)
		{
			XmlQualifiedName xmlQualifiedName = new XmlQualifiedName("Person", "http://Test");
			//XmlTypeHelper.AddSchema(schemas, xmlQualifiedName);
			return xmlQualifiedName;
		}
	}

	// Token: 0x02000157 RID: 343
	[ServiceContract(Namespace = "http://microsoft.samples", Name = "IXmlSerializerContract")]
	public interface IXmlSerializerContract
	{
		// Token: 0x060008D8 RID: 2264
		[XmlSerializerFormat]
		[OperationContract]
		Task<XmlSerializerPerson> GetPerson();
	}
}
