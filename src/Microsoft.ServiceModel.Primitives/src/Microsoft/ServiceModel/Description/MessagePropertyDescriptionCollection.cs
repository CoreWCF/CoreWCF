namespace Microsoft.ServiceModel.Description
{
    public class MessagePropertyDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<string, MessagePropertyDescription>
    {
        internal MessagePropertyDescriptionCollection() { }
        protected override string GetKeyForItem(MessagePropertyDescription item) { return default(string); }
    }
}