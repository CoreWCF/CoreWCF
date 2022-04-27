// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Security;
using System.Xml;

namespace CoreWCF.Configuration
{
    public abstract class ServiceModelExtensionCollectionElement<TServiceModelExtensionElement> : ServiceModelConfigurationElement, ICollection<TServiceModelExtensionElement>
        where TServiceModelExtensionElement : ServiceModelExtensionElement
    {
        private readonly string _extensionCollectionName;
        private bool _modified;
        private List<TServiceModelExtensionElement> _items;

        internal ServiceModelExtensionCollectionElement(string extensionCollectionName)
        {
            _extensionCollectionName = extensionCollectionName;
        }

        public TServiceModelExtensionElement this[int index]
        {
            get { return Items[index]; }
        }

        public TServiceModelExtensionElement this[Type extensionType]
        {
            get
            {
                if (extensionType == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(extensionType));
                }

                if (!CollectionElementBaseType.IsAssignableFrom(extensionType))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("extensionType",
                        SR.Format(SR.ConfigInvalidExtensionType,
                        extensionType.ToString(),
                        CollectionElementBaseType.FullName,
                        _extensionCollectionName));

                }
                TServiceModelExtensionElement retval = null;

                foreach (TServiceModelExtensionElement collectionElement in this)
                {
                    if (null != collectionElement)
                    {
                        if (collectionElement.GetType() == extensionType)
                        {
                            retval = collectionElement;
                        }
                    }
                }

                return retval;
            }
        }

        public int Count
        {
            get { return Items.Count; }
        }

        bool ICollection<TServiceModelExtensionElement>.IsReadOnly
        {
            get { return IsReadOnly(); }
        }

        internal List<TServiceModelExtensionElement> Items
        {
            get
            {
                if (_items == null)
                {
                    _items = new List<TServiceModelExtensionElement>();
                }
                return _items;
            }
        }

        public virtual void Add(TServiceModelExtensionElement element)
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            element.ExtensionCollectionName = _extensionCollectionName;

            if (Contains(element))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("element", SR.Format(SR.ConfigDuplicateKey, element.ConfigurationElementName));
            }
            else if (!CanAdd(element))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("element",
                    SR.Format(SR.ConfigElementTypeNotAllowed,
                    element.ConfigurationElementName,
                    _extensionCollectionName));
            }
            else
            {
                ConfigurationProperty configProperty = new ConfigurationProperty(element.ConfigurationElementName, element.GetType(), null);
                Properties.Add(configProperty);
                this[configProperty] = element;
                Items.Add(element);
                _modified = true;
            }
        }

        internal void AddItem(TServiceModelExtensionElement element)
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            element.ExtensionCollectionName = _extensionCollectionName;
            Items.Add(element);
            _modified = true;
        }

        public virtual bool CanAdd(TServiceModelExtensionElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            Type elementType = element.GetType();

            return !IsReadOnly() && !ContainsKey(elementType);
        }

        public void Clear()
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (Properties.Count > 0)
            {
                _modified = true;
            }

            List<string> propertiesToRemove = new List<string>(Items.Count);
            foreach (TServiceModelExtensionElement item in Items)
            {
                propertiesToRemove.Add(item.ConfigurationElementName);
            }

            Items.Clear();

            foreach (string name in propertiesToRemove)
            {
                Properties.Remove(name);
            }
        }

        internal Type CollectionElementBaseType
        {
            get { return typeof(TServiceModelExtensionElement); }
        }

        public bool Contains(TServiceModelExtensionElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }
            return ContainsKey(element.GetType());
        }

        public bool ContainsKey(Type elementType)
        {
            if (elementType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elementType));
            }
            return (this[elementType] != null);
        }

        public bool ContainsKey(string elementName)
        {
            if (string.IsNullOrEmpty(elementName))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elementName));
            }
            bool retval = false;
            foreach (TServiceModelExtensionElement element in this)
            {
                if (null != element)
                {
                    string configuredSectionName = element.ConfigurationElementName;
                    if (configuredSectionName.Equals(elementName, StringComparison.Ordinal))
                    {
                        retval = true;
                        break;
                    }
                }
            }
            return retval;
        }

        public void CopyTo(TServiceModelExtensionElement[] elements, int start)
        {
            if (elements == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));
            }
            if (start < 0 || start >= elements.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("start",
                    SR.Format(SR.ConfigInvalidStartValue,
                    elements.Length - 1,
                    start));
            }

            foreach (TServiceModelExtensionElement element in this)
            {
                if (null != element)
                {
                    string configuredSectionName = element.ConfigurationElementName;

                    TServiceModelExtensionElement copiedElement = CreateNewSection(configuredSectionName);
                    if ((copiedElement != null) && (start < elements.Length))
                    {
                        copiedElement.CopyFrom(element);
                        elements[start] = copiedElement;
                        ++start;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the extension element, or null if the type cannot be loaded in certain situations (see the code for details).
        /// </summary>
        private TServiceModelExtensionElement CreateNewSection(string name)
        {
            if (ContainsKey(name) && !(name == ConfigurationStrings.Clear || name == ConfigurationStrings.Remove))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigDuplicateItem,
                    name,
                    GetType().Name),
                    ElementInformation.Source,
                    ElementInformation.LineNumber));
            }

            TServiceModelExtensionElement retval;

            Type elementType;
            ContextInformation evaluationContext = EvaluationContext;

            elementType = GetExtensionType(evaluationContext, name);


            if (null != elementType)
            {
                if (CollectionElementBaseType.IsAssignableFrom(elementType))
                {
                    retval = (TServiceModelExtensionElement)Activator.CreateInstance(elementType);
                    retval.ExtensionCollectionName = _extensionCollectionName;
                    retval.ConfigurationElementName = name;
                    retval.InternalInitializeDefault();
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigInvalidExtensionElement,
                        name,
                        CollectionElementBaseType.FullName),
                        ElementInformation.Source,
                        ElementInformation.LineNumber));
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigInvalidExtensionElementName,
                    name,
                    _extensionCollectionName),
                    ElementInformation.Source,
                    ElementInformation.LineNumber));
            }

            return retval;
        }

        
        private Type GetExtensionType(ContextInformation evaluationContext, string name)
        {
            ExtensionElementCollection collection = ExtensionsSection.LookupCollection(_extensionCollectionName, evaluationContext);
            if (collection.ContainsKey(name))
            {
                ExtensionElement element = collection.GetElementExtension(name);
                Type elementType = Type.GetType(element.Type, false);
                if (null == elementType)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigInvalidType, element.Type, element.Name),
                        ElementInformation.Source,
                        ElementInformation.LineNumber));
                }
                return elementType;
            }
            return null;
        }

        internal void MergeWith(List<TServiceModelExtensionElement> parentExtensionElements)
        {
            Merge(parentExtensionElements, this);
            Clear();
            foreach (TServiceModelExtensionElement parentExtensionElement in parentExtensionElements)
            {
                Add(parentExtensionElement);
            }
        }

        private static void Merge(List<TServiceModelExtensionElement> parentExtensionElements, IEnumerable<TServiceModelExtensionElement> childExtensionElements)
        {
            foreach (TServiceModelExtensionElement childExtensionElement in childExtensionElements)
            {
                Type childExtensionElementType = childExtensionElement.GetType();
                parentExtensionElements.RemoveAll(element =>
                    element != null && element.GetType() == childExtensionElementType);
                parentExtensionElements.Add(childExtensionElement);
            }
        }

        
        protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElementCore(reader);
        }

        private void DeserializeElementCore(XmlReader reader)
        {
            if (reader.HasAttributes && 0 < reader.AttributeCount)
            {
                while (reader.MoveToNextAttribute())
                {
                    if (Properties.Contains(reader.Name))
                    {
                        this[reader.Name] = Properties[reader.Name].Converter.ConvertFromString(reader.Value);
                    }
                    else
                    {
                        OnDeserializeUnrecognizedAttribute(reader.Name, reader.Value);
                    }
                }
            }

            if (XmlNodeType.Element != reader.NodeType)
            {
                reader.MoveToElement();
            }

            XmlReader subTree = reader.ReadSubtree();
            if (subTree.Read())
            {
                while (subTree.Read())
                {
                    if (XmlNodeType.Element == subTree.NodeType)
                    {
                        // Create new child element and add it to the property collection to
                        // associate the element with an EvaluationContext.  Then deserialize
                        // XML further to set actual values.
                        TServiceModelExtensionElement collectionElement = CreateNewSection(subTree.Name);
                        if (collectionElement != null)
                        {
                            Add(collectionElement);
                            collectionElement.DeserializeInternal(subTree, false);
                        }
                    }
                }
            }
        }

        public IEnumerator<TServiceModelExtensionElement> GetEnumerator()
        {
            for (int index = 0; index < Items.Count; ++index)
            {
                TServiceModelExtensionElement currentValue = _items[index];
                yield return currentValue;
            }
        }

        protected override bool IsModified()
        {
            bool retval = _modified;
            if (!retval)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    TServiceModelExtensionElement element = Items[i];
                    if (element.IsModifiedInternal())
                    {
                        retval = true;
                        break;
                    }
                }
            }
            return retval;
        }

        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            // When this is used as a DefaultCollection (i.e. CommonBehaviors)
            // the element names are unrecognized by the parent tag, which delegates
            // to the collection's OnDeserializeUnrecognizedElement.  In this case,
            // an unrecognized element may be expected, simply try to deserialize the
            // element and let DeserializeElement() throw the appropriate exception if
            // an error is hit.
            DeserializeElement(reader, false);
            return true;
        }

        public bool Remove(TServiceModelExtensionElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }
            bool retval = false;
            if (Contains(element))
            {
                string configuredSectionName = element.ConfigurationElementName;

                TServiceModelExtensionElement existingElement = this[element.GetType()];
                Items.Remove(existingElement);
                Properties.Remove(configuredSectionName);
                _modified = true;
                retval = true;
            }
            return retval;
        }

        protected override void Reset(ConfigurationElement parentElement)
        {
            ServiceModelExtensionCollectionElement<TServiceModelExtensionElement> collection =
                (ServiceModelExtensionCollectionElement<TServiceModelExtensionElement>)parentElement;
            foreach (TServiceModelExtensionElement collectionElement in collection.Items)
            {
                Items.Add(collectionElement);
            }
            // Update my properties
            UpdateProperties(collection);

            base.Reset(parentElement);
        }

        protected override void ResetModified()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                TServiceModelExtensionElement collectionElement = Items[i];
                collectionElement.ResetModifiedInternal();
            }
            _modified = false;
        }

        protected void SetIsModified()
        {
            _modified = true;
        }

        protected override void SetReadOnly()
        {
            base.SetReadOnly();

            for (int i = 0; i < Items.Count; i++)
            {
                TServiceModelExtensionElement element = Items[i];
                element.SetReadOnlyInternal();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected override void Unmerge(ConfigurationElement sourceElement, ConfigurationElement parentElement, ConfigurationSaveMode saveMode)
        {
            if (sourceElement == null)
            {
                return;
            }

            ServiceModelExtensionCollectionElement<TServiceModelExtensionElement> sourceCollectionElement = (ServiceModelExtensionCollectionElement<TServiceModelExtensionElement>)sourceElement;

            UpdateProperties(sourceCollectionElement);
            base.Unmerge(sourceElement, parentElement, saveMode);
        }

        private void UpdateProperties(ServiceModelExtensionCollectionElement<TServiceModelExtensionElement> sourceElement)
        {
            foreach (ConfigurationProperty property in sourceElement.Properties)
            {
                if (!Properties.Contains(property.Name))
                {
                    Properties.Add(property);
                }
            }

            foreach (TServiceModelExtensionElement extension in Items)
            {
                string configuredSectionName = extension.ConfigurationElementName;
                if (!Properties.Contains(configuredSectionName))
                {
                    ConfigurationProperty configProperty = new ConfigurationProperty(configuredSectionName, extension.GetType(), null);
                    Properties.Add(configProperty);
                }
            }
        }
    }
}
