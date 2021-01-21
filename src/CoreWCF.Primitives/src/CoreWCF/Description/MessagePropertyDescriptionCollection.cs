// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public class MessagePropertyDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<string, MessagePropertyDescription>
    {
        internal MessagePropertyDescriptionCollection() { }
        protected override string GetKeyForItem(MessagePropertyDescription item) { return default(string); }
    }
}