// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.Xml.Schema;
using System.Reflection;
using System.Runtime.Serialization;

namespace CoreWCF.Runtime.Serialization
{
    internal class XsdDataContractExporterEx
    {
        XmlSchemaSet _schemas;
        DataContractSetEx _dataContractSet;

        public XsdDataContractExporterEx()
        {
        }

        public XsdDataContractExporterEx(XmlSchemaSet schemas)
        {
            _schemas = schemas;
        }

        XmlSchemaSet GetSchemaSet()
        {
            if (_schemas == null)
            {
                _schemas = new XmlSchemaSet();
                _schemas.XmlResolver = null;
            }
            return _schemas;
        }

        DataContractSetEx DataContractSet
        {
            get
            {
                if (_dataContractSet == null)
                {
                    _dataContractSet = new DataContractSetEx();
                }
                return _dataContractSet;
            }
        }

        public void Export(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));

            DataContractSetEx oldValue = (_dataContractSet == null) ? null : new DataContractSetEx(_dataContractSet);
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

                _dataContractSet = oldValue;
                throw;
            }
        }

        public XmlQualifiedName GetSchemaTypeName(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));

            type = GetSurrogatedType(type);
            DataContractEx dataContract = DataContractEx.GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(dataContract.UnderlyingType);
            if (dataContract.IsXmlDataContract && dataContract.XmlDataContractIsAnonymous)
                return XmlQualifiedName.Empty;
            return dataContract.StableName;
        }

        public XmlSchemaType GetSchemaType(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));

            type = GetSurrogatedType(type);
            DataContractEx dataContract = DataContractEx.GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(dataContract.UnderlyingType);
            if (dataContract.IsXmlDataContract && dataContract.XmlDataContractIsAnonymous)
                return dataContract.XmlDataContractXsdType;
            return null;
        }

        public XmlQualifiedName GetRootElementName(Type type)
        {
            if (type == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(type)));

            type = GetSurrogatedType(type);
            DataContractEx dataContract = DataContractEx.GetDataContract(type);
            DataContractSetEx.EnsureTypeNotGeneric(dataContract.UnderlyingType);
            if (dataContract.HasRoot)
            {
                return new XmlQualifiedName(dataContract.TopLevelElementName.Value, dataContract.TopLevelElementNamespace.Value);
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

        void AddType(Type type)
        {
            DataContractSet.Add(type);
        }

        void Export()
        {
            DataContractSet.FixupEnumDataContracts();
            var schemaExporterType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.SchemaExporter");
            var schemaExporter = Activator.CreateInstance(schemaExporterType,
                                                          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                                                          null,
                                                          new object[] { GetSchemaSet(), DataContractSet.Wrapped },
                                                          null,
                                                          null);
            var exportMethod = schemaExporterType.GetMethod("Export", BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
            exportMethod.Invoke(schemaExporter, null);
        }
    }
}
