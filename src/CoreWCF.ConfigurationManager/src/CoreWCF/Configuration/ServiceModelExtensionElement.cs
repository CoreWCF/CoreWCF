// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Security.Permissions;
using System.Xml;

namespace CoreWCF.Configuration
{
    [ConfigurationPermission(SecurityAction.InheritanceDemand, Unrestricted = true)]
    public abstract class ServiceModelExtensionElement : ServiceModelConfigurationElement
    {
        public string ExtensionCollectionName { get; protected internal set; }
        public string ConfigurationElementName { get; protected internal set; }

        internal void InternalInitializeDefault()
        {
            this.InitializeDefault();
        }

        public virtual void CopyFrom(ServiceModelExtensionElement from)
        {
            if (this.IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(string.Format(SR.ConfigReadOnly)));
            }
            if (from == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("from");
            }
        }
        
        internal void DeserializeInternal(XmlReader reader, bool serializeCollectionKey)
        {
            this.DeserializeElement(reader, serializeCollectionKey);
        }

        internal bool IsModifiedInternal()
        {
            return this.IsModified();
        }

        internal void ResetModifiedInternal()
        {
            this.ResetModified();
        }

        internal void SetReadOnlyInternal()
        {
            this.SetReadOnly();
        }
    }
}
