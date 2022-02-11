// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;

namespace CoreWCF.Description
{
    public class PolicyAssertionCollection : Collection<XmlElement>
    {
        public PolicyAssertionCollection()
        {
        }

        public PolicyAssertionCollection(IEnumerable<XmlElement> elements)
        {
            if (elements == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));
            }

            AddRange(elements);
        }

        internal void AddRange(IEnumerable<XmlElement> elements)
        {
            foreach (XmlElement element in elements)
            {
                Add(element);
            }
        }

        public bool Contains(string localName, string namespaceUri)
        {
            if (localName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(localName));
            }

            if (namespaceUri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(namespaceUri));
            }

            for (int i = 0; i < Count; i++)
            {
                XmlElement item = this[i];
                if (item.LocalName == localName && item.NamespaceURI == namespaceUri)
                {
                    return true;
                }
            }

            return false;
        }

        public XmlElement Find(string localName, string namespaceUri)
        {
            return Find(localName, namespaceUri, false);
        }

        public XmlElement Remove(string localName, string namespaceUri)
        {
            return Find(localName, namespaceUri, true);
        }

        XmlElement Find(string localName, string namespaceUri, bool remove)
        {
            if (localName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(localName));
            }

            if (namespaceUri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull((namespaceUri));
            }

            for (int index = 0; index < Count; index++)
            {
                XmlElement item = this[index];
                if (item.LocalName == localName && item.NamespaceURI == namespaceUri)
                {
                    if (remove)
                    {
                        RemoveAt(index);
                    }
                    return item;
                }
            }

            return null;
        }

        public Collection<XmlElement> FindAll(string localName, string namespaceUri)
        {
            return FindAll(localName, namespaceUri, false);
        }

        public Collection<XmlElement> RemoveAll(string localName, string namespaceUri)
        {
            return FindAll(localName, namespaceUri, true);
        }

        Collection<XmlElement> FindAll(string localName, string namespaceUri, bool remove)
        {
            if (localName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(localName));
            }

            if (namespaceUri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(namespaceUri));
            }

            Collection<XmlElement> collection = new Collection<XmlElement>();

            for (int index = 0; index < Count; index++)
            {
                XmlElement item = this[index];
                if (item.LocalName == localName && item.NamespaceURI == namespaceUri)
                {
                    if (remove)
                    {
                        RemoveAt(index);
                        // back up the index so we inspect the new item at this location
                        index--;
                    }
                    collection.Add(item);
                }
            }

            return collection;
        }

        protected override void InsertItem(int index, XmlElement item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, XmlElement item)
        {
            if (item == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
            }

            base.SetItem(index, item);
        }
    }
}
