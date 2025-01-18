// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Security.Permissions;
using System.Xml;

namespace CoreWCF.Configuration
{
    public abstract class ServiceModelExtensionElement : ServiceModelConfigurationElement
    {
        public string ExtensionCollectionName { get; protected internal set; }
        public string ConfigurationElementName { get; protected internal set; }

        internal void InternalInitializeDefault()
        {
            InitializeDefault();
        }

        public virtual void CopyFrom(ServiceModelExtensionElement from)
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(string.Format(SR.ConfigReadOnly)));
            }
            if (from == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(from));
            }
        }

        internal void DeserializeInternal(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElement(reader, serializeCollectionKey);
        }

        internal bool IsModifiedInternal()
        {
            return IsModified();
        }

        internal void ResetModifiedInternal()
        {
            ResetModified();
        }

        internal void SetReadOnlyInternal()
        {
            SetReadOnly();
        }
    }
}
