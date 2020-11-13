using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;

namespace CoreWCF.Security
{
    internal class SendSecurityHeaderElement
    {
        private bool markedForEncryption;

        public SendSecurityHeaderElement(string id, ISecurityElement item)
        {
            this.Id = id;
            this.Item = item;
            markedForEncryption = false;
        }

        public string Id { get; private set; }

        public ISecurityElement Item { get; private set; }

        public bool MarkedForEncryption
        {
            get { return this.markedForEncryption; }
            set { this.markedForEncryption = value; }
        }

        public bool IsSameItem(ISecurityElement item)
        {
            return this.Item == item || this.Item.Equals(item);
        }

        public void Replace(string id, ISecurityElement item)
        {
            this.Item = item;
            this.Id = id;
        }
    }
}
