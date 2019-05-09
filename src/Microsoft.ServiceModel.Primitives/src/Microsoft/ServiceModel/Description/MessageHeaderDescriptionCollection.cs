namespace Microsoft.ServiceModel.Description
{
    public class MessageHeaderDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<System.Xml.XmlQualifiedName, MessageHeaderDescription>
    {
        internal MessageHeaderDescriptionCollection() { }
        protected override System.Xml.XmlQualifiedName GetKeyForItem(MessageHeaderDescription item) { return default(System.Xml.XmlQualifiedName); }
    }
}