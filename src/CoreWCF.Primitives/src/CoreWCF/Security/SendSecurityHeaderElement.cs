// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;

namespace CoreWCF.Security
{
    internal class SendSecurityHeaderElement
    {
        public SendSecurityHeaderElement(string id, ISecurityElement item)
        {
            Id = id;
            Item = item;
            MarkedForEncryption = false;
        }

        public string Id { get; private set; }

        public ISecurityElement Item { get; private set; }

        public bool MarkedForEncryption { get; set; }

        public bool IsSameItem(ISecurityElement item)
        {
            return Item == item || Item.Equals(item);
        }

        public void Replace(string id, ISecurityElement item)
        {
            Item = item;
            Id = id;
        }
    }
}
