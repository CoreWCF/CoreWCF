// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Globalization;
using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF.Configuration
{
    internal sealed class ExtensionsSection : ConfigurationSection
    {

        private static readonly Lazy<ExtensionsSection> s_extensionsSection = new Lazy<ExtensionsSection>(LazyThreadSafetyMode.ExecutionAndPublication);

        private static ExtensionsSection ExtensionSection => s_extensionsSection.Value;

        [ConfigurationProperty(ConfigurationStrings.BindingElementExtensions)]
        public ExtensionElementCollection BindingElementExtensions
        {
            get { return (ExtensionElementCollection)base[ConfigurationStrings.BindingElementExtensions]; }
        }

        [ConfigurationProperty(ConfigurationStrings.BindingExtensions)]
        public ExtensionElementCollection BindingExtensions
        {
            get { return (ExtensionElementCollection)base[ConfigurationStrings.BindingExtensions]; }
        }

        void InitializeBindingElementExtensions()
        {
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.BinaryMessageEncodingSectionName, typeof(BinaryMessageEncodingElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.HttpsTransportSectionName, typeof(HttpsTransportElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.HttpTransportSectionName, typeof(HttpTransportElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.MtomMessageEncodingSectionName, typeof(MtomMessageEncodingElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.SecuritySectionName, typeof(SecurityElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.SslStreamSecuritySectionName, typeof(SslStreamSecurityElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.TcpTransportSectionName, typeof(TcpTransportElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.TextMessageEncodingSectionName, typeof(TextMessageEncodingElement).AssemblyQualifiedName));
            this.BindingElementExtensions.Add(new ExtensionElement(ConfigurationStrings.WindowsStreamSecuritySectionName, typeof(WindowsStreamSecurityElement).AssemblyQualifiedName));

        }

        void InitializeBindingExtensions()
        {
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.BasicHttpBindingCollectionElementName, typeof(BasicHttpBindingCollectionElement).AssemblyQualifiedName));
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.CustomBindingCollectionElementName, typeof(CustomBindingCollectionElement).AssemblyQualifiedName));
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.NetTcpBindingCollectionElementName, typeof(NetTcpBindingCollectionElement).AssemblyQualifiedName));
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.WSHttpBindingCollectionElementName, typeof(WSHttpBindingCollectionElement).AssemblyQualifiedName));
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.UdpBindingCollectionElementName, ConfigurationStrings.UdpBindingCollectionElementType));
            this.BindingExtensions.Add(new ExtensionElement(ConfigurationStrings.NetHttpBindingCollectionElementName, typeof(NetHttpBindingCollectionElement).AssemblyQualifiedName));

        }

        public ExtensionsSection()
        {
            InitializeDefault();
        }

        protected override void InitializeDefault()
        {
            this.InitializeBindingElementExtensions();
            this.InitializeBindingExtensions();
        }


        internal static ExtensionElementCollection LookupAssociatedCollection(Type extensionType, ContextInformation evaluationContext, out string collectionName)
        {
            collectionName = GetExtensionType(extensionType);
            return ExtensionsSection.LookupCollection(collectionName, evaluationContext);
        }
        
        static string GetExtensionType(Type extensionType)
        {
            string collectionName = string.Empty;

            if (extensionType.IsSubclassOf(typeof(BindingElementExtensionElement)))
            {
                collectionName = ConfigurationStrings.BindingElementExtensions;
            }
            else if (extensionType.IsSubclassOf(typeof(BindingCollectionElement)))
            {
                collectionName = ConfigurationStrings.BindingExtensions;
            }
            else
            {
                // LookupAssociatedCollection built on assumption that extensionType is valid.
                // This should be protected at the callers site.  If assumption is invalid, then
                // configuration system is in an indeterminate state.  Need to stop in a manner that
                // user code can not capture.
                Fx.Assert(string.Format(CultureInfo.InvariantCulture, "{0} is not a type supported by the ServiceModelExtensionsSection collections.", extensionType.AssemblyQualifiedName));
                DiagnosticUtility.FailFast(string.Format(CultureInfo.InvariantCulture, "{0} is not a type supported by the ServiceModelExtensionsSection collections.", extensionType.AssemblyQualifiedName));
            }

            return collectionName;
        }


        internal static ExtensionElementCollection LookupCollection(string collectionName, ContextInformation evaluationContext)
        {
            ExtensionElementCollection collection = null;
            ExtensionsSection extensionsSection = ExtensionSection;

           

            switch (collectionName)
            {
                case (ConfigurationStrings.BindingElementExtensions):
                    collection = extensionsSection.BindingElementExtensions;
                    break;
                case (ConfigurationStrings.BindingExtensions):
                    collection = extensionsSection.BindingExtensions;
                    break;
                default:
                    // LookupCollection built on assumption that collectionName is valid.
                    // This should be protected at the callers site.  If assumption is invalid, then
                    // configuration system is in an indeterminate state.  Need to stop in a manner that
                    // user code can not capture.
                    Fx.Assert(string.Format(CultureInfo.InvariantCulture, "{0} is not a valid ServiceModelExtensionsSection collection name.", collectionName));
                    DiagnosticUtility.FailFast(string.Format(CultureInfo.InvariantCulture, "{0} is not a valid ServiceModelExtensionsSection collection name.", collectionName));
                    break;
            }

            return collection;
        }
    }
}
