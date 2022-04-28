// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Description
{
    public class MessagePartDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<XmlQualifiedName, MessagePartDescription>
    {
        internal MessagePartDescriptionCollection() { }
        protected override XmlQualifiedName GetKeyForItem(MessagePartDescription item) => new XmlQualifiedName(item.Name, item.Namespace);
    }
}
