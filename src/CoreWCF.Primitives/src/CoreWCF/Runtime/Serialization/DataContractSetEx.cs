// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Runtime.Serialization;
using System.Xml;

namespace CoreWCF.Runtime.Serialization
{
    internal class DataContractSetEx
    {
        internal const string RemoveKeyValuePairFromWsdl = "CoreWCF.RemoveKeyValuePairFromWsdl";
        internal bool _removeKeyValuePairFromWsdl = AppContext.TryGetSwitch(RemoveKeyValuePairFromWsdl, out bool enabled) && enabled;

        internal DataContractSetEx()
        {
            Wrapped = FormatterServices.GetUninitializedObject(s_dataContractSetType);
        }

        internal DataContractSetEx(DataContractSetEx dataContractSet) : this()
        {
            foreach (var key in dataContractSet.Contracts.Keys)
            {
                Contracts[key] = dataContractSet.Contracts[key];
            }

            foreach (var key in dataContractSet.ProcessedContracts.Keys)
            {
                ProcessedContracts[key] = dataContractSet.ProcessedContracts[key];
            }
        }

        public object Wrapped { get; }

        public IDictionary Contracts => s_getContracts(Wrapped);

        public IDictionary ProcessedContracts => s_getProcessedContracts(Wrapped);

        internal void Add(Type type)
        {
            DataContractEx dataContract = DataContractEx.GetDataContract(type);
            EnsureTypeNotGeneric(dataContract.UnderlyingType);
            Add(dataContract);
        }

        internal static void EnsureTypeNotGeneric(Type type)
        {
            if (type.ContainsGenericParameters)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(SR.Format(SR.GenericTypeNotExportable, type)));
        }

        private void Add(DataContractEx dataContract)
        {
            Add(dataContract.StableName, dataContract);
        }

        public void Add(XmlQualifiedName name, DataContractEx dataContract)
        {
            if (dataContract.IsBuiltInDataContract)
                return;
            InternalAdd(name, dataContract);
        }

        internal void InternalAdd(XmlQualifiedName name, DataContractEx dataContract)
        {
            DataContractEx dataContractInSet;
            if (Contracts.Contains(name))
            {
                dataContractInSet = DataContractEx.Wrap(Contracts[name]);
                if (!dataContractInSet.Equals(dataContract.WrappedDataContract))
                {
                    if (dataContract.UnderlyingType == null || dataContractInSet.UnderlyingType == null)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DupContractInDataContractSet, dataContract.StableName.Name, dataContract.StableName.Namespace)));
                    else
                    {
                        bool typeNamesEqual = (DataContractEx.GetClrTypeFullName(dataContract.UnderlyingType) == DataContractEx.GetClrTypeFullName(dataContractInSet.UnderlyingType));
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DupTypeContractInDataContractSet, (typeNamesEqual ? dataContract.UnderlyingType.AssemblyQualifiedName : DataContractEx.GetClrTypeFullName(dataContract.UnderlyingType)), (typeNamesEqual ? dataContractInSet.UnderlyingType.AssemblyQualifiedName : DataContractEx.GetClrTypeFullName(dataContractInSet.UnderlyingType)), dataContract.StableName.Name, dataContract.StableName.Namespace)));
                    }
                }
            }
            else
            {
                Contracts.Add(name, dataContract.WrappedDataContract);

                if (dataContract is ClassDataContractEx classDataContract)
                {
                    AddClassDataContract(classDataContract);
                }
                else if (dataContract is CollectionDataContractEx collectionDataContract)
                {
                    AddCollectionDataContract(collectionDataContract);
                }
                else if (dataContract is XmlDataContractEx xmlDataContract)
                {
                    AddXmlDataContract(xmlDataContract);
                }
            }
        }

        private void AddClassDataContract(ClassDataContractEx classDataContract)
        {
            if (classDataContract.BaseContract != null)
            {
                Add(classDataContract.BaseContract.StableName, classDataContract.BaseContract);
            }
            if (!classDataContract.IsISerializable)
            {
                if (classDataContract.Members != null)
                {
                    for (int i = 0; i < classDataContract.Members.Count; i++)
                    {
                        DataMemberEx dataMember = classDataContract.Members[i];
                        DataContractEx memberDataContract = GetMemberTypeDataContract(dataMember);
                        //if (dataContractSurrogate != null && dataMember.MemberInfo != null)
                        //{
                        //    object customData = DataContractSurrogateCaller.GetCustomDataToExport(
                        //                           dataContractSurrogate,
                        //                           dataMember.MemberInfo,
                        //                           memberDataContract.UnderlyingType);
                        //    if (customData != null)
                        //        SurrogateDataTable.Add(dataMember, customData);
                        //}
                        Add(memberDataContract.StableName, memberDataContract);
                    }
                }
            }
            AddKnownDataContracts(classDataContract.KnownDataContracts);
        }

        private void AddCollectionDataContract(CollectionDataContractEx collectionDataContract)
        {
            if (collectionDataContract.IsDictionary)
            {
                ClassDataContractEx keyValueContract = collectionDataContract.ItemContract as ClassDataContractEx;
                AddClassDataContract(keyValueContract);
            }
            else
            {
                DataContractEx itemContract = GetItemTypeDataContract(collectionDataContract);
                if (itemContract != null)
                    Add(itemContract.StableName, itemContract);
            }
            AddKnownDataContracts(collectionDataContract.KnownDataContracts);
        }

        private void AddXmlDataContract(XmlDataContractEx xmlDataContract)
        {
            AddKnownDataContracts(xmlDataContract.KnownDataContracts);
        }

        private void AddKnownDataContracts(IDictionary knownDataContracts)
        {
            if (knownDataContracts != null)
            {
                foreach (object knownDataContract in knownDataContracts.Values)
                {
                    var dataContract = DataContractEx.Wrap(knownDataContract);
                    // Workaround for DataContract adding an extra schema entry for KeyValue<K,V>. See GitHub
                    // issue https://github.com/dotnet/runtime/issues/67949 for details.
                    if (_removeKeyValuePairFromWsdl && IsKeyValuePair(dataContract))
                        continue;

                    Add(dataContract);
                }
            }
        }

        private bool IsKeyValuePair(DataContractEx dataContract)
        {
            var stableName = dataContract.StableName;
            return stableName.Namespace == "http://schemas.datacontract.org/2004/07/System.Collections.Generic"
                                           && stableName.Name.StartsWith("KeyValuePairOf");
        }

        internal DataContractEx GetMemberTypeDataContract(DataMemberEx dataMember)
        {
            if (dataMember.MemberInfo != null)
            {
                Type dataMemberType = dataMember.MemberType;
                if (dataMember.IsGetOnlyCollection)
                {
                    //if (dataContractSurrogate != null)
                    //{
                    //    Type dcType = DataContractSurrogateCaller.GetDataContractType(dataContractSurrogate, dataMemberType);
                    //    if (dcType != dataMemberType)
                    //    {
                    //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(SR.GetString(SR.SurrogatesWithGetOnlyCollectionsNotSupported,
                    //            DataContract.GetClrTypeFullName(dataMemberType), DataContract.GetClrTypeFullName(dataMember.MemberInfo.DeclaringType), dataMember.MemberInfo.Name)));
                    //    }
                    //}
                    return DataContractEx.GetGetOnlyCollectionDataContract(DataContractEx.GetId(dataMemberType.TypeHandle), dataMemberType.TypeHandle, dataMemberType, /*SerializationMode.SharedContract*/ 0);
                }
                else
                {
                    return DataContractEx.GetDataContract(dataMemberType);
                }
            }
            return dataMember.MemberTypeContract;
        }

        internal DataContractEx GetItemTypeDataContract(CollectionDataContractEx collectionContract)
        {
            if (collectionContract.ItemType != null)
                return DataContractEx.GetDataContract(collectionContract.ItemType);
            return collectionContract.ItemContract;
        }

        internal void FixupDataContracts()
        {
            // This fixes the Enum data contract to have an underlying type
            // and for collections of KeyValuePairAdapter to have IsValueType set to true
            // and the key and value members to be required. This is done by ensuring all
            // types get wrapped at least once. The wrapping constructor fixes up the
            // necessary data.
            foreach(object contractObj in Contracts.Values)
            {
                DataContractEx.Wrap(contractObj);
            }
        }

        private static Type s_dataContractSetType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContractSet");
        private static Func<object, IDictionary> s_getContracts = ReflectionHelper.GetPropertyDelegate<IDictionary>(s_dataContractSetType, "Contracts");
        private static Func<object, IDictionary> s_getProcessedContracts = ReflectionHelper.GetPropertyDelegate<IDictionary>(s_dataContractSetType, "ProcessedContracts");
    }
}
