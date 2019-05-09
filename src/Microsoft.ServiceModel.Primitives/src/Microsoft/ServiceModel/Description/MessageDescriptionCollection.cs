namespace Microsoft.ServiceModel.Description
{
    public class MessageDescriptionCollection : System.Collections.ObjectModel.Collection<MessageDescription>
    {
        internal MessageDescriptionCollection() { }
        public MessageDescription Find(string action) { return default(MessageDescription); }
        public System.Collections.ObjectModel.Collection<MessageDescription> FindAll(string action) { return default(System.Collections.ObjectModel.Collection<MessageDescription>); }
    }
}