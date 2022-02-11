// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Xml.Schema;
using System.Xml;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
using CoreWCF.Runtime;

namespace CoreWCF.Xml.Serialization
{
    /// <include file='doc\SoapSchemaExporter.uex' path='docs/doc[@for="SoapSchemaExporter"]/*' />
    /// <internalonly/>
    public class SoapSchemaExporter
    {
        internal const XmlSchemaForm elementFormDefault = XmlSchemaForm.Qualified;
        XmlSchemas schemas;
        Hashtable types = new Hashtable();      // StructMapping/EnumMapping -> XmlSchemaComplexType/XmlSchemaSimpleType
        bool exportedRoot;
        object scope;
        XmlDocument document;

        static XmlQualifiedName ArrayQName = new XmlQualifiedName(Soap.Array, Soap.Encoding);
        static XmlQualifiedName ArrayTypeQName = new XmlQualifiedName(Soap.ArrayType, Soap.Encoding);

        /// <include file='doc\SoapSchemaExporter.uex' path='docs/doc[@for="SoapSchemaExporter.SoapSchemaExporter"]/*' />
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public SoapSchemaExporter(XmlSchemas schemas)
        {
            this.schemas = schemas;
        }

        //        /// <include file='doc\SoapSchemaExporter.uex' path='docs/doc[@for="SoapSchemaExporter.ExportTypeMapping"]/*' />
        //        /// <devdoc>
        //        ///    <para>[To be supplied.]</para>
        //        /// </devdoc>
        //        public void ExportTypeMapping(XmlTypeMapping xmlTypeMapping)
        //        {
        //            CheckScope(xmlTypeMapping.Scope);
        //            ExportTypeMapping(xmlTypeMapping.Mapping, null);
        //        }

        /// <include file='doc\SoapSchemaExporter.uex' path='docs/doc[@for="SoapSchemaExporter.ExportMembersMapping"]/*' />
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public void ExportMembersMapping(XmlMembersMapping xmlMembersMapping)
        {
            ExportMembersMapping(xmlMembersMapping, false);
        }

        /// <include file='doc\SoapSchemaExporter.uex' path='docs/doc[@for="SoapSchemaExporter.ExportMembersMapping1"]/*' />
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public void ExportMembersMapping(XmlMembersMapping xmlMembersMapping, bool exportEnclosingType)
        {
            CheckScope(xmlMembersMapping.GetScope());
            var membersMapping = xmlMembersMapping.GetAccessor().Mapping.AsMembersMappingSurrogate();
            if (exportEnclosingType)
            {
                Fx.Assert("Not expecting to export enclosing type");
                //ExportTypeMapping(membersMapping, null);
            }
            else
            {
                foreach (MemberMappingSurrogate memberMapping in membersMapping.Members)
                {
                    if (memberMapping.Elements.Length > 0)
                        ExportTypeMapping(memberMapping.Elements[0].Mapping, null);
                }
            }
        }

        void CheckScope(object scope)
        {
            if (this.scope == null)
            {
                this.scope = scope;
            }
            else if (this.scope != scope)
            {
                throw new InvalidOperationException(SR.XmlMappingsScopeMismatch);
            }
        }

        internal XmlDocument Document
        {
            get
            {
                if (document == null) document = new XmlDocument();
                return document;
            }
        }

        void CheckForDuplicateType(string newTypeName, string newNamespace)
        {
            XmlSchema schema = schemas[newNamespace];
            if (schema != null)
            {
                foreach (XmlSchemaObject o in schema.Items)
                {
                    XmlSchemaType type = o as XmlSchemaType;
                    if (type != null && type.Name == newTypeName)
                        throw new InvalidOperationException(SR.Format(SR.XmlDuplicateTypeName, newTypeName, newNamespace));

                }
            }
        }

        void AddSchemaItem(XmlSchemaObject item, string ns, string referencingNs)
        {
            if (!SchemaContainsItem(item, ns))
            {
                XmlSchema schema = schemas[ns];
                if (schema == null)
                {
                    schema = new XmlSchema();
                    schema.TargetNamespace = ns == null || ns.Length == 0 ? null : ns;
                    schema.ElementFormDefault = elementFormDefault;
                    schemas.Add(schema);
                }
                schema.Items.Add(item);
            }
            if (referencingNs != null)
                AddSchemaImport(ns, referencingNs);
        }

        void AddSchemaImport(string ns, string referencingNs)
        {
            if (referencingNs == null || ns == null) return;
            if (ns == referencingNs) return;
            XmlSchema schema = schemas[referencingNs];
            if (schema == null) throw new InvalidOperationException(SR.Format(SR.XmlMissingSchema, referencingNs));
            if (ns.Length > 0 && FindImport(schema, ns) == null)
            {
                XmlSchemaImport import = new XmlSchemaImport();
                import.Namespace = ns;
                schema.Includes.Add(import);
            }
        }

        bool SchemaContainsItem(XmlSchemaObject item, string ns)
        {
            XmlSchema schema = schemas[ns];
            if (schema != null)
            {
                return schema.Items.Contains(item);
            }
            return false;
        }

        XmlSchemaImport FindImport(XmlSchema schema, string ns)
        {
            foreach (object item in schema.Includes)
            {
                if (item is XmlSchemaImport)
                {
                    XmlSchemaImport import = (XmlSchemaImport)item;
                    if (import.Namespace == ns)
                    {
                        return import;
                    }
                }
            }
            return null;
        }

        XmlQualifiedName ExportTypeMapping(MappingSurrogate mapping, string ns)
        {
            if (mapping.IsArrayMapping)
                return ExportArrayMapping(mapping.AsArrayMappingSurrogate(), ns);
            else if (mapping.IsEnumMapping)
                return ExportEnumMapping(mapping.AsEnumMappingSurrogate(), ns);
            else if (mapping.IsPrimitiveMapping)
            {
                PrimitiveMappingSurrogate pm = mapping.AsPrimitiveMappingSurrogate();
                if (pm.TypeDesc.IsXsdType)
                {
                    return ExportPrimitiveMapping(pm);
                }
                else
                {
                    return ExportNonXsdPrimitiveMapping(pm, ns);
                }
            }
            else if (mapping.IsStructMapping)
                return ExportStructMapping(mapping.AsStructMappingSurrogate(), ns);
            else if (mapping.IsNullableMapping)
                return ExportTypeMapping(mapping.AsNullableMappingSurrogate().BaseMapping, ns);
            else if (mapping.IsMembersMapping)
                return ExportMembersMapping(mapping.AsMembersMappingSurrogate(), ns);
            else
                throw new ArgumentException(SR.XmlInternalError, "mapping");
        }

        XmlQualifiedName ExportNonXsdPrimitiveMapping(PrimitiveMappingSurrogate mapping, string ns)
        {
            XmlSchemaType type = mapping.TypeDesc.DataType;
            if (!SchemaContainsItem(type, UrtTypes.Namespace))
            {
                AddSchemaItem(type, UrtTypes.Namespace, ns);
            }
            else
            {
                AddSchemaImport(UrtTypes.Namespace, ns);
            }
            return new XmlQualifiedName(mapping.TypeDesc.DataType.Name, UrtTypes.Namespace);
        }

        XmlQualifiedName ExportPrimitiveMapping(PrimitiveMappingSurrogate mapping)
        {
            return new XmlQualifiedName(mapping.TypeDesc.DataType.Name, XmlSchema.Namespace);
        }

        XmlQualifiedName ExportArrayMapping(ArrayMappingSurrogate mapping, string ns)
        {
            // for the Rpc ArrayMapping different mappings could have the same schema type
            // we link all mappings corresponding to the same type together
            // loop through all mapping that will map to the same complexType, and export only one, 
            // the obvious choice is the last one.
            while (mapping.Next != null)
            {
                mapping = mapping.Next;
            }

            XmlSchemaComplexType type = (XmlSchemaComplexType)types[mapping];
            if (type == null)
            {
                CheckForDuplicateType(mapping.TypeName, mapping.Namespace);
                type = new XmlSchemaComplexType();
                type.Name = mapping.TypeName;
                types.Add(mapping, type);

                // we need to add the type first, to make sure that the schema get created
                AddSchemaItem(type, mapping.Namespace, ns);
                AddSchemaImport(Soap.Encoding, mapping.Namespace);
                AddSchemaImport(Wsdl.Namespace, mapping.Namespace);

                XmlSchemaComplexContentRestriction restriction = new XmlSchemaComplexContentRestriction();
                XmlQualifiedName qname = ExportTypeMapping(mapping.Elements[0].Mapping, mapping.Namespace);

                if (qname.IsEmpty)
                {
                    // this is a root mapping
                    qname = new XmlQualifiedName(Soap.UrType, XmlSchema.Namespace);
                }
                //<attribute ref="soapenc:arrayType" wsdl:arrayType="xsd:float[]"/> 
                XmlSchemaAttribute attr = new XmlSchemaAttribute();
                attr.RefName = ArrayTypeQName;
                XmlAttribute attribute = XmlMembersMappingExtensions.CreateXmlAttribute("wsdl", Wsdl.ArrayType, Wsdl.Namespace, Document);
                attribute.Value = qname.Namespace + ":" + qname.Name + "[]";

                attr.UnhandledAttributes = new XmlAttribute[] { attribute };
                restriction.Attributes.Add(attr);
                restriction.BaseTypeName = ArrayQName;
                XmlSchemaComplexContent model = new XmlSchemaComplexContent();
                model.Content = restriction;
                type.ContentModel = model;
                if (qname.Namespace != XmlSchema.Namespace)
                    AddSchemaImport(qname.Namespace, mapping.Namespace);
            }
            else
            {
                AddSchemaImport(mapping.Namespace, ns);
            }
            return new XmlQualifiedName(mapping.TypeName, mapping.Namespace);
        }

        void ExportElementAccessors(XmlSchemaGroupBase group, ElementAccessorSurrogate[] accessors, bool repeats, bool valueTypeOptional, string ns)
        {
            if (accessors.Length == 0) return;
            if (accessors.Length == 1)
            {
                ExportElementAccessor(group, accessors[0], repeats, valueTypeOptional, ns);
            }
            else
            {
                XmlSchemaChoice choice = new XmlSchemaChoice();
                choice.MaxOccurs = repeats ? decimal.MaxValue : 1;
                choice.MinOccurs = repeats ? 0 : 1;
                for (int i = 0; i < accessors.Length; i++)
                    ExportElementAccessor(choice, accessors[i], false, valueTypeOptional, ns);

                if (choice.Items.Count > 0) group.Items.Add(choice);
            }
        }


        void ExportElementAccessor(XmlSchemaGroupBase group, ElementAccessorSurrogate accessor, bool repeats, bool valueTypeOptional, string ns)
        {
            XmlSchemaElement element = new XmlSchemaElement();
            element.MinOccurs = repeats || valueTypeOptional ? 0 : 1;
            element.MaxOccurs = repeats ? decimal.MaxValue : 1;
            element.Name = accessor.Name;
            element.IsNillable = accessor.IsNullable || accessor.Mapping.IsNullableMapping;
            element.Form = XmlSchemaForm.Unqualified;
            element.SchemaTypeName = ExportTypeMapping(accessor.Mapping, accessor.Namespace);

            group.Items.Add(element);
        }

        XmlQualifiedName ExportRootMapping(StructMappingSurrogate mapping)
        {
            if (!exportedRoot)
            {
                exportedRoot = true;
                ExportDerivedMappings(mapping);
            }
            return new XmlQualifiedName(Soap.UrType, XmlSchema.Namespace);
        }

        XmlQualifiedName ExportStructMapping(StructMappingSurrogate mapping, string ns)
        {
            if (mapping.TypeDesc.IsRoot) return ExportRootMapping(mapping);
            XmlSchemaComplexType type = (XmlSchemaComplexType)types[mapping];
            if (type == null)
            {
                if (!mapping.IncludeInSchema) throw new InvalidOperationException(SR.Format(SR.XmlSoapCannotIncludeInSchema, mapping.TypeDesc.Name));
                CheckForDuplicateType(mapping.TypeName, mapping.Namespace);
                type = new XmlSchemaComplexType();
                type.Name = mapping.TypeName;
                types.Add(mapping, type);
                AddSchemaItem(type, mapping.Namespace, ns);
                type.IsAbstract = mapping.TypeDesc.IsAbstract;

                if (mapping.BaseMapping != null && mapping.BaseMapping.IncludeInSchema)
                {
                    XmlSchemaComplexContentExtension extension = new XmlSchemaComplexContentExtension();
                    extension.BaseTypeName = ExportStructMapping(mapping.BaseMapping, mapping.Namespace);
                    XmlSchemaComplexContent model = new XmlSchemaComplexContent();
                    model.Content = extension;
                    type.ContentModel = model;
                }
                ExportTypeMembers(type, mapping.Members, mapping.Namespace);
                ExportDerivedMappings(mapping);
            }
            else
            {
                AddSchemaImport(mapping.Namespace, ns);
            }
            return new XmlQualifiedName(type.Name, mapping.Namespace);
        }

        XmlQualifiedName ExportMembersMapping(MembersMappingSurrogate mapping, string ns)
        {
            XmlSchemaComplexType type = (XmlSchemaComplexType)types[mapping];
            if (type == null)
            {
                CheckForDuplicateType(mapping.TypeName, mapping.Namespace);
                type = new XmlSchemaComplexType();
                type.Name = mapping.TypeName;
                types.Add(mapping, type);
                AddSchemaItem(type, mapping.Namespace, ns);
                ExportTypeMembers(type, mapping.Members, mapping.Namespace);
            }
            else
            {
                AddSchemaImport(mapping.Namespace, ns);
            }
            return new XmlQualifiedName(type.Name, mapping.Namespace);
        }

        void ExportTypeMembers(XmlSchemaComplexType type, MemberMappingSurrogate[] members, string ns)
        {
            XmlSchemaGroupBase seq = new XmlSchemaSequence();
            for (int i = 0; i < members.Length; i++)
            {
                MemberMappingSurrogate member = members[i];
                if (member.Elements.Length > 0)
                {
                    bool valueTypeOptional = member.CheckSpecified != MemberMappingSurrogate.SpecifiedAccessor.None || member.CheckShouldPersist || !member.TypeDesc.IsValueType;
                    ExportElementAccessors(seq, member.Elements, false, valueTypeOptional, ns);
                }
            }
            if (seq.Items.Count > 0)
            {
                if (type.ContentModel != null)
                {
                    if (type.ContentModel.Content is XmlSchemaComplexContentExtension)
                        ((XmlSchemaComplexContentExtension)type.ContentModel.Content).Particle = seq;
                    else if (type.ContentModel.Content is XmlSchemaComplexContentRestriction)
                        ((XmlSchemaComplexContentRestriction)type.ContentModel.Content).Particle = seq;
                    else
                        throw new InvalidOperationException(SR.Format(SR.XmlInvalidContent, type.ContentModel.Content.GetType().Name));
                }
                else
                {
                    type.Particle = seq;
                }
            }
        }

        void ExportDerivedMappings(StructMappingSurrogate mapping)
        {
            for (StructMappingSurrogate derived = mapping.DerivedMappings; derived != null; derived = derived.NextDerivedMapping)
            {
                if (derived.IncludeInSchema) ExportStructMapping(derived, mapping.TypeDesc.IsRoot ? null : mapping.Namespace);
            }
        }

        XmlQualifiedName ExportEnumMapping(EnumMappingSurrogate mapping, string ns)
        {
            XmlSchemaSimpleType dataType = (XmlSchemaSimpleType)types[mapping];
            if (dataType == null)
            {
                CheckForDuplicateType(mapping.TypeName, mapping.Namespace);
                dataType = new XmlSchemaSimpleType();
                dataType.Name = mapping.TypeName;
                types.Add(mapping, dataType);
                AddSchemaItem(dataType, mapping.Namespace, ns);

                XmlSchemaSimpleTypeRestriction restriction = new XmlSchemaSimpleTypeRestriction();
                restriction.BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace);

                for (int i = 0; i < mapping.Constants.Length; i++)
                {
                    ConstantMappingSurrogate constant = mapping.Constants[i];
                    XmlSchemaEnumerationFacet enumeration = new XmlSchemaEnumerationFacet();
                    enumeration.Value = constant.XmlName;
                    restriction.Facets.Add(enumeration);
                }
                if (!mapping.IsFlags)
                {
                    dataType.Content = restriction;
                }
                else
                {
                    XmlSchemaSimpleTypeList list = new XmlSchemaSimpleTypeList();
                    XmlSchemaSimpleType enumType = new XmlSchemaSimpleType();
                    enumType.Content = restriction;
                    list.ItemType = enumType;
                    dataType.Content = list;
                }
            }
            else
            {
                AddSchemaImport(mapping.Namespace, ns);
            }
            return new XmlQualifiedName(mapping.TypeName, mapping.Namespace);
        }
    }

    internal class Soap
    {
        private Soap() { }
        internal const string Encoding = "http://schemas.xmlsoap.org/soap/encoding/";
        internal const string UrType = "anyType";
        internal const string Array = "Array";
        internal const string ArrayType = "arrayType";
    }

    //internal class Soap12
    //{
    //    private Soap12() { }
    //    internal const string Encoding = "http://www.w3.org/2003/05/soap-encoding";
    //    internal const string RpcNamespace = "http://www.w3.org/2003/05/soap-rpc";
    //    internal const string RpcResult = "result";
    //}

    internal class Wsdl
    {
        private Wsdl() { }
        internal const string Namespace = "http://schemas.xmlsoap.org/wsdl/";
        internal const string ArrayType = "arrayType";
    }

    internal class UrtTypes
    {
        private UrtTypes() { }
        internal const string Namespace = "http://microsoft.com/wsdl/types/";
    }
}
