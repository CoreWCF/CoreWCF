// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace CoreWCF.Runtime.Serialization
{
    internal class XsdDataContractExporterEx
    {
        //ExportOptions options;
        XmlSchemaSet schemas;
        DataContractSetEx dataContractSet;

        public XsdDataContractExporterEx()
        {
        }

        public XsdDataContractExporterEx(XmlSchemaSet schemas)
        {
            this.schemas = schemas;
        }

        //public ExportOptions Options
        //{
        //    get { return options; }
        //    set { options = value; }
        //}

        //public XmlSchemaSet Schemas
        //{
        //    get
        //    {
        //        XmlSchemaSet schemaSet = GetSchemaSet();
        //        SchemaImporter.CompileSchemaSet(schemaSet);
        //        return schemaSet;
        //    }
        //}

        XmlSchemaSet GetSchemaSet()
        {
            if (schemas == null)
            {
                schemas = new XmlSchemaSet();
                schemas.XmlResolver = null;
            }
            return schemas;
        }

        DataContractSetEx DataContractSet
        {
            get
            {
                if (dataContractSet == null)
                {
                    dataContractSet = new DataContractSetEx();
                }
                return dataContractSet;
            }
        }

        //void TraceExportBegin()
        //{
        //    if (DiagnosticUtility.ShouldTraceInformation)
        //    {
        //        TraceUtility.Trace(TraceEventType.Information, TraceCode.XsdExportBegin, SR.GetString(SR.TraceCodeXsdExportBegin));
        //    }
        //}

        //void TraceExportEnd()
        //{
        //    if (DiagnosticUtility.ShouldTraceInformation)
        //    {
        //        TraceUtility.Trace(TraceEventType.Information, TraceCode.XsdExportEnd, SR.GetString(SR.TraceCodeXsdExportEnd));
        //    }
        //}

        //void TraceExportError(Exception exception)
        //{
        //    if (DiagnosticUtility.ShouldTraceError)
        //    {
        //        TraceUtility.Trace(TraceEventType.Error, TraceCode.XsdExportError, SR.GetString(SR.TraceCodeXsdExportError), null, exception);
        //    }
        //}

        //public void Export(ICollection<Assembly> assemblies)
        //{
        //    if (assemblies == null)
        //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("assemblies"));

        //    TraceExportBegin();

        //    DataContractSet oldValue = (dataContractSet == null) ? null : new DataContractSet(dataContractSet);
        //    try
        //    {
        //        foreach (Assembly assembly in assemblies)
        //        {
        //            if (assembly == null)
        //                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.GetString(SR.CannotExportNullAssembly, "assemblies")));

        //            Type[] types = assembly.GetTypes();
        //            for (int j = 0; j < types.Length; j++)
        //                CheckAndAddType(types[j]);
        //        }

        //        Export();
        //    }
        //    catch (Exception ex)
        //    {
        //        if (Fx.IsFatal(ex))
        //        {
        //            throw;
        //        }
        //        dataContractSet = oldValue;
        //        TraceExportError(ex);
        //        throw;
        //    }
        //    TraceExportEnd();
        //}

        //public void Export(ICollection<Type> types)
        //{
        //    if (types == null)
        //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("types"));

        //    TraceExportBegin();

        //    DataContractSet oldValue = (dataContractSet == null) ? null : new DataContractSet(dataContractSet);
        //    try
        //    {
        //        foreach (Type type in types)
        //        {
        //            if (type == null)
        //                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.GetString(SR.CannotExportNullType, "types")));
        //            AddType(type);
        //        }

        //        Export();
        //    }
        //    catch (Exception ex)
        //    {
        //        if (Fx.IsFatal(ex))
        //        {
        //            throw;
        //        }
        //        dataContractSet = oldValue;
        //        TraceExportError(ex);
        //        throw;
        //    }
        //    TraceExportEnd();
        //}

        public void Export(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));

            //TraceExportBegin();

            DataContractSetEx oldValue = (dataContractSet == null) ? null : new DataContractSetEx(dataContractSet);
            try
            {
                AddType(type);
                Export();
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }
                dataContractSet = oldValue;
                //TraceExportError(ex);
                throw;
            }
            //TraceExportEnd();
        }

        public XmlQualifiedName GetSchemaTypeName(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));
            type = GetSurrogatedType(type);
            object dataContract = GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(UnderlyingType(dataContract));
            if (dataContract != null && IsXmlDataContract(dataContract) && XmlDataContractIsAnonymous(dataContract))
                return XmlQualifiedName.Empty;
            return StableName(dataContract);
        }

        public XmlSchemaType GetSchemaType(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));
            type = GetSurrogatedType(type);
            object dataContract = GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(UnderlyingType(dataContract));
            if (dataContract != null && IsXmlDataContract(dataContract) && XmlDataContractIsAnonymous(dataContract))
                return XmlDataContractXsdType(dataContract);
            return null;
        }

        public XmlQualifiedName GetRootElementName(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));
            type = GetSurrogatedType(type);
            object dataContract = GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(UnderlyingType(dataContract));
            if (HasRoot(dataContract))
            {
                return new XmlQualifiedName(TopLevelElementName(dataContract).Value, TopLevelElementNamespace(dataContract).Value);
            }
            else
            {
                return null;
            }
        }

        Type GetSurrogatedType(Type type)
        {
            //IDataContractSurrogate dataContractSurrogate;
            //if (options != null && (dataContractSurrogate = Options.GetSurrogate()) != null)
            //    type = DataContractSurrogateCaller.GetDataContractType(dataContractSurrogate, type);
            return type;
        }

        //void CheckAndAddType(Type type)
        //{
        //    type = GetSurrogatedType(type);
        //    if (!type.ContainsGenericParameters && DataContract.IsTypeSerializable(type))
        //        AddType(type);
        //}

        void AddType(Type type)
        {
            DataContractSet.Add(type);
        }

        void Export()
        {
            var schemaExporterType = typeof(System.Runtime.Serialization.DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.SchemaExporter");
            var schemaExporter = Activator.CreateInstance(schemaExporterType,
                                                          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                                                          null,
                                                          new object[] { GetSchemaSet(), DataContractSet.Wrapped },
                                                          null,
                                                          null);
            var exportMethod = schemaExporterType.GetMethod("Export", BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
            exportMethod.Invoke(schemaExporter, null);
        }

        //public bool CanExport(ICollection<Assembly> assemblies)
        //{
        //    if (assemblies == null)
        //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("assemblies"));

        //    DataContractSet oldValue = (dataContractSet == null) ? null : new DataContractSet(dataContractSet);
        //    try
        //    {
        //        foreach (Assembly assembly in assemblies)
        //        {
        //            if (assembly == null)
        //                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.GetString(SR.CannotExportNullAssembly, "assemblies")));

        //            Type[] types = assembly.GetTypes();
        //            for (int j = 0; j < types.Length; j++)
        //                CheckAndAddType(types[j]);
        //        }
        //        AddKnownTypes();
        //        return true;
        //    }
        //    catch (InvalidDataContractException)
        //    {
        //        dataContractSet = oldValue;
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (Fx.IsFatal(ex))
        //        {
        //            throw;
        //        }
        //        dataContractSet = oldValue;
        //        TraceExportError(ex);
        //        throw;
        //    }
        //}

        //public bool CanExport(ICollection<Type> types)
        //{
        //    if (types == null)
        //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("types"));

        //    DataContractSet oldValue = (dataContractSet == null) ? null : new DataContractSet(dataContractSet);
        //    try
        //    {
        //        foreach (Type type in types)
        //        {
        //            if (type == null)
        //                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.GetString(SR.CannotExportNullType, "types")));
        //            AddType(type);
        //        }
        //        AddKnownTypes();
        //        return true;
        //    }
        //    catch (InvalidDataContractException)
        //    {
        //        dataContractSet = oldValue;
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (Fx.IsFatal(ex))
        //        {
        //            throw;
        //        }
        //        dataContractSet = oldValue;
        //        TraceExportError(ex);
        //        throw;
        //    }
        //}

        //public bool CanExport(Type type)
        //{
        //    if (type == null)
        //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("type"));

        //    DataContractSet oldValue = (dataContractSet == null) ? null : new DataContractSet(dataContractSet);
        //    try
        //    {
        //        AddType(type);
        //        AddKnownTypes();
        //        return true;
        //    }
        //    catch (InvalidDataContractException)
        //    {
        //        dataContractSet = oldValue;
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (Fx.IsFatal(ex))
        //        {
        //            throw;
        //        }
        //        dataContractSet = oldValue;
        //        TraceExportError(ex);
        //        throw;
        //    }
        //}

        #region Helpers
        internal object GetDataContract(Type clrType)
        {
            //GetMethod(string name, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers);
            var methodInfo = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract")
                .GetMethod("GetDataContract", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(Type) }, null);
            return methodInfo.Invoke(null, new object[] { clrType });
        }

        private static Type UnderlyingType(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("UnderlyingType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (Type)property.GetValue(dataContract);
        }

        private static XmlQualifiedName StableName(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("StableName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (XmlQualifiedName)property.GetValue(dataContract);
        }

        private static XmlDictionaryString TopLevelElementName(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("TopLevelElementName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (XmlDictionaryString)property.GetValue(dataContract);
        }

        private static XmlDictionaryString TopLevelElementNamespace(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("TopLevelElementNamespace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (XmlDictionaryString)property.GetValue(dataContract);
        }

        private static bool HasRoot(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("HasRoot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (bool)property.GetValue(dataContract);
        }

        private static bool XmlDataContractIsAnonymous(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.XmlDataContract").GetProperty("IsAnonymous", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)property.GetValue(dataContract);
        }

        private static XmlSchemaType XmlDataContractXsdType(object dataContract)
        {
            var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.XmlDataContract").GetProperty("XsdType", BindingFlags.Instance | BindingFlags.NonPublic);
            return (XmlSchemaType)property.GetValue(dataContract);
        }

        private static bool IsXmlDataContract(object dataContract) => dataContract.GetType().FullName.Equals("System.Runtime.Serialization.XmlDataContract");
        #endregion
    }
}
