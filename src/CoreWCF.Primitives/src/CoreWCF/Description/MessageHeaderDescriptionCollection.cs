// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Description
{
    public class MessageHeaderDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<XmlQualifiedName, MessageHeaderDescription>
    {
        internal MessageHeaderDescriptionCollection() { }
        protected override XmlQualifiedName GetKeyForItem(MessageHeaderDescription item) => new XmlQualifiedName(item.Name, item.Namespace);
    }
}
