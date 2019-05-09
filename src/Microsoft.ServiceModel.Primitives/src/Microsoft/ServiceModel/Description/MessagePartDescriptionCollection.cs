namespace Microsoft.ServiceModel.Description
{
    public class MessagePartDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<System.Xml.XmlQualifiedName, MessagePartDescription>
    {
        internal MessagePartDescriptionCollection() { }
        protected override System.Xml.XmlQualifiedName GetKeyForItem(MessagePartDescription item) { return default(System.Xml.XmlQualifiedName); }
    }
}