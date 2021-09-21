// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DataContractDictionary = System.Collections.Generic.Dictionary<System.Xml.XmlQualifiedName, object>;
using System.Runtime.Serialization;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CoreWCF.Runtime.Serialization
{
    internal class DataContractSetEx
    {
        private static Type s_dataContractSetType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContractSet");

        //DataContractDictionary contracts;
        //Dictionary<object, object> processedContracts;
        //IDataContractSurrogate dataContractSurrogate;
        //Hashtable surrogateDataTable;
        //DataContractDictionary knownTypesForObject;
        //ICollection<Type> referencedTypes;
        //ICollection<Type> referencedCollectionTypes;
        //DataContractDictionary referencedTypesDictionary;
        //DataContractDictionary referencedCollectionTypesDictionary;

        //internal DataContractSetEx(IDataContractSurrogate dataContractSurrogate) : this(dataContractSurrogate, null, null) { }

        //internal DataContractSetEx(IDataContractSurrogate dataContractSurrogate, ICollection<Type> referencedTypes, ICollection<Type> referencedCollectionTypes)
        //{
        //    this.dataContractSurrogate = dataContractSurrogate;
        //    this.referencedTypes = referencedTypes;
        //    this.referencedCollectionTypes = referencedCollectionTypes;
        //}

        internal DataContractSetEx()
        {
            Wrapped = FormatterServices.GetUninitializedObject(s_dataContractSetType);
        }

        internal DataContractSetEx(DataContractSetEx dataContractSet)
        {
            //CreateInstance(Type type, BindingFlags bindingAttr, Binder binder, object[] args, CultureInfo culture);
            Wrapped = Activator.CreateInstance(s_dataContractSetType,
                                               BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                                               null,
                                               new object[] { dataContractSet.Wrapped },
                                               null);
            //if (dataContractSet == null)
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(dataContractSet)));

            ////this.dataContractSurrogate = dataContractSet.dataContractSurrogate;
            //this.referencedTypes = dataContractSet.referencedTypes;
            //this.referencedCollectionTypes = dataContractSet.referencedCollectionTypes;

            //foreach (KeyValuePair<XmlQualifiedName, object> pair in dataContractSet)
            //{
            //    Add(pair.Key, pair.Value);
            //}

            //if (dataContractSet.processedContracts != null)
            //{
            //    foreach (KeyValuePair<object, object> pair in dataContractSet.processedContracts)
            //    {
            //        ProcessedContracts.Add(pair.Key, pair.Value);
            //    }
            //}
        }

        public object Wrapped { get; }

        internal void Add(Type type)
        {
            var addTypeMethodInfo = s_dataContractSetType.GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Type) }, null);
            addTypeMethodInfo.Invoke(Wrapped, new object[] { type });
        }


        //DataContractDictionary Contracts
        //{
        //    get
        //    {
        //        if (contracts == null)
        //        {
        //            contracts = new DataContractDictionary();
        //        }
        //        return contracts;
        //    }
        //}

        //Dictionary<object, object> ProcessedContracts
        //{
        //    get
        //    {
        //        if (processedContracts == null)
        //        {
        //            processedContracts = new Dictionary<object, object>();
        //        }
        //        return processedContracts;
        //    }
        //}

        //////Hashtable SurrogateDataTable
        //////{
        //////    get
        //////    {
        //////        if (surrogateDataTable == null)
        //////            surrogateDataTable = new Hashtable();
        //////        return surrogateDataTable;
        //////    }
        //////}

        ////internal DataContractDictionary KnownTypesForObject
        ////{
        ////    get { return knownTypesForObject; }
        ////    set { knownTypesForObject = value; }
        ////}

        ////internal void Add(Type type)
        ////{
        ////    DataContract dataContract = GetDataContract(type);
        ////    EnsureTypeNotGeneric(dataContract.UnderlyingType);
        ////    Add(dataContract);
        ////}

        internal static void EnsureTypeNotGeneric(Type type)
        {
            if (type.ContainsGenericParameters)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(SR.Format(SR.GenericTypeNotExportable, type)));
        }

        //void Add(object dataContract)
        //{
        //    Add(StableName(dataContract), dataContract);
        //}

        //public void Add(XmlQualifiedName name, object dataContract)
        //{
        //    if (IsBuiltInDataContract(dataContract))
        //        return;
        //    InternalAdd(name, dataContract);
        //}

        //internal void InternalAdd(XmlQualifiedName name, object dataContract)
        //{
        //    object dataContractInSet = null;
        //    if (Contracts.TryGetValue(name, out dataContractInSet))
        //    {
        //        if (!dataContractInSet.Equals(dataContract))
        //        {
        //            if (UnderlyingType(dataContract) == null || UnderlyingType(dataContractInSet) == null)
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DupContractInDataContractSet, StableName(dataContract).Name, StableName(dataContract).Namespace)));
        //            else
        //            {
        //                bool typeNamesEqual = (GetClrTypeFullName(UnderlyingType(dataContract)) == GetClrTypeFullName(UnderlyingType(dataContractInSet)));
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DupTypeContractInDataContractSet, (typeNamesEqual ? UnderlyingType(dataContract).AssemblyQualifiedName : GetClrTypeFullName(UnderlyingType(dataContract))), (typeNamesEqual ? UnderlyingType(dataContractInSet).AssemblyQualifiedName : GetClrTypeFullName(UnderlyingType(dataContractInSet))), StableName(dataContract).Name, StableName(dataContract).Namespace)));
        //            }
        //        }
        //    }
        //    else
        //    {
        //        Contracts.Add(name, dataContract);

        //        if (IsClassDataContract(dataContract))
        //        {
        //            AddClassDataContract(dataContract);
        //        }
        //        else if (IsCollectionDataContract(dataContract))
        //        {
        //            AddCollectionDataContract(dataContract);
        //        }
        //        else if (IsXmlDataContract(dataContract))
        //        {
        //            AddXmlDataContract(dataContract);
        //        }
        //    }
        //}

        //void AddClassDataContract(object classDataContract)
        //{
        //    var baseContract = ClassDataContractBaseContract(classDataContract);
        //    if (baseContract != null)
        //    {
        //        Add(StableName(baseContract), baseContract);
        //    }
        //    if (!IsISerializable(classDataContract))
        //    {
        //        IList members = ClassDataContractMembers(classDataContract);
        //        if (members != null)
        //        {
        //            for (int i = 0; i < members.Count; i++)
        //            {
        //                object dataMember = members[i];
        //                object memberDataContract = GetMemberTypeDataContract(dataMember);
        //                Add(StableName(memberDataContract), memberDataContract);
        //            }
        //        }
        //    }
        //    AddKnownDataContracts(ClassDataContractKnownDataContracts(classDataContract));
        //}


        //void AddCollectionDataContract(object collectionDataContract)
        //{
        //    if (CollectionDataContractIsDictionary(collectionDataContract))
        //    {
        //        object keyValueContract = CollectionDataContractItemContract(collectionDataContract);
        //        AddClassDataContract(keyValueContract);
        //    }
        //    else
        //    {
        //        object itemContract = GetItemTypeDataContract(collectionDataContract);
        //        if (itemContract != null)
        //            Add(StableName(itemContract), itemContract);
        //    }
        //    AddKnownDataContracts(CollectionDataContractKnownDataContracts(collectionDataContract));
        //}

        //void AddXmlDataContract(object xmlDataContract)
        //{
        //    AddKnownDataContracts(XmlDataContractKnownDataContracts(xmlDataContract));
        //}

        //void AddKnownDataContracts(DataContractDictionary knownDataContracts)
        //{
        //    if (knownDataContracts != null)
        //    {
        //        foreach (object knownDataContract in knownDataContracts.Values)
        //        {
        //            Add(knownDataContract);
        //        }
        //    }
        //}

        ////internal XmlQualifiedName GetStableName(Type clrType)
        ////{
        ////    if (dataContractSurrogate != null)
        ////    {
        ////        Type dcType = DataContractSurrogateCaller.GetDataContractType(dataContractSurrogate, clrType);

        ////        //if (clrType.IsValueType != dcType.IsValueType)
        ////        //    throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(SR.GetString(SR.ValueTypeMismatchInSurrogatedType, dcType, clrType)));
        ////        return DataContract.GetStableName(dcType);
        ////    }
        ////    return DataContract.GetStableName(clrType);
        ////}

        //internal object GetDataContract(Type clrType)
        //{
        //    var methodInfo = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetMethod("GetDataContract", BindingFlags.Static | BindingFlags.NonPublic);
        //    return methodInfo.Invoke(null, new object[] { clrType });
        //}

        //internal object GetMemberTypeDataContract(object dataMember)
        //{
        //    var memberInfo = DataMemberMemberInfo(dataMember);
        //    if (memberInfo != null)
        //    {
        //        Type dataMemberType = MemberType(memberInfo);
        //        if (DataMemberIsGetOnlyCollection(dataMember))
        //        {
        //            //if (dataContractSurrogate != null)
        //            //{
        //            //    Type dcType = DataContractSurrogateCaller.GetDataContractType(dataContractSurrogate, dataMemberType);
        //            //    if (dcType != dataMemberType)
        //            //    {
        //            //        throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(SR.GetString(SR.SurrogatesWithGetOnlyCollectionsNotSupported,
        //            //            DataContract.GetClrTypeFullName(dataMemberType), DataContract.GetClrTypeFullName(dataMember.MemberInfo.DeclaringType), dataMember.MemberInfo.Name)));
        //            //    }
        //            //}
        //            return GetGetOnlyCollectionDataContract(GetId(dataMemberType.TypeHandle), dataMemberType.TypeHandle, dataMemberType, /*SerializationMode.SharedContract*/ 0);
        //        }
        //        else
        //        {
        //            return GetDataContract(dataMemberType);
        //        }
        //    }
        //    return DataMemberMemberTypeContract(dataMember);
        //}

        //internal object GetItemTypeDataContract(object collectionContract)
        //{
        //    Type collectionContractItemType = CollectionDataContractItemType(collectionContract);
        //    if (collectionContractItemType != null)
        //        return GetDataContract(collectionContractItemType);

        //    return collectionContractItemType;
        //}

        ////internal object GetSurrogateData(object key)
        ////{
        ////    return SurrogateDataTable[key];
        ////}

        ////internal void SetSurrogateData(object key, object surrogateData)
        ////{
        ////    SurrogateDataTable[key] = surrogateData;
        ////}

        ////public DataContract this[XmlQualifiedName key]
        ////{
        ////    get
        ////    {
        ////        DataContract dataContract = DataContract.GetBuiltInDataContract(key.Name, key.Namespace);
        ////        if (dataContract == null)
        ////        {
        ////            Contracts.TryGetValue(key, out dataContract);
        ////        }
        ////        return dataContract;
        ////    }
        ////}

        ////public IDataContractSurrogate DataContractSurrogate
        ////{
        ////    get { return dataContractSurrogate; }
        ////}

        ////public bool Remove(XmlQualifiedName key)
        ////{
        ////    if (DataContract.GetBuiltInDataContract(key.Name, key.Namespace) != null)
        ////        return false;
        ////    return Contracts.Remove(key);
        ////}

        //public IEnumerator<KeyValuePair<XmlQualifiedName, object>> GetEnumerator()
        //{
        //    return Contracts.GetEnumerator();
        //}

        ////internal bool IsContractProcessed(DataContract dataContract)
        ////{
        ////    return ProcessedContracts.ContainsKey(dataContract);
        ////}

        ////internal void SetContractProcessed(DataContract dataContract)
        ////{
        ////    ProcessedContracts.Add(dataContract, dataContract);
        ////}

        ////internal ContractCodeDomInfo GetContractCodeDomInfo(DataContract dataContract)
        ////{
        ////    object info;
        ////    if (ProcessedContracts.TryGetValue(dataContract, out info))
        ////        return (ContractCodeDomInfo)info;
        ////    return null;
        ////}

        ////internal void SetContractCodeDomInfo(DataContract dataContract, ContractCodeDomInfo info)
        ////{
        ////    ProcessedContracts.Add(dataContract, info);
        ////}
        ////Dictionary<XmlQualifiedName, object> GetReferencedTypes()
        ////{
        ////    if (referencedTypesDictionary == null)
        ////    {
        ////        referencedTypesDictionary = new Dictionary<XmlQualifiedName, object>();
        ////        //Always include Nullable as referenced type
        ////        //Do not allow surrogating Nullable<T>
        ////        referencedTypesDictionary.Add(DataContract.GetStableName(Globals.TypeOfNullable), Globals.TypeOfNullable);
        ////        if (this.referencedTypes != null)
        ////        {
        ////            foreach (Type type in this.referencedTypes)
        ////            {
        ////                if (type == null)
        ////                    throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.ReferencedTypesCannotContainNull)));

        ////                AddReferencedType(referencedTypesDictionary, type);
        ////            }
        ////        }
        ////    }
        ////    return referencedTypesDictionary;
        ////}

        ////Dictionary<XmlQualifiedName, object> GetReferencedCollectionTypes()
        ////{
        ////    if (referencedCollectionTypesDictionary == null)
        ////    {
        ////        referencedCollectionTypesDictionary = new Dictionary<XmlQualifiedName, object>();
        ////        if (this.referencedCollectionTypes != null)
        ////        {
        ////            foreach (Type type in this.referencedCollectionTypes)
        ////            {
        ////                if (type == null)
        ////                    throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.ReferencedCollectionTypesCannotContainNull)));
        ////                AddReferencedType(referencedCollectionTypesDictionary, type);
        ////            }
        ////        }
        ////        XmlQualifiedName genericDictionaryName = DataContract.GetStableName(Globals.TypeOfDictionaryGeneric);
        ////        if (!referencedCollectionTypesDictionary.ContainsKey(genericDictionaryName) && GetReferencedTypes().ContainsKey(genericDictionaryName))
        ////            AddReferencedType(referencedCollectionTypesDictionary, Globals.TypeOfDictionaryGeneric);
        ////    }
        ////    return referencedCollectionTypesDictionary;
        ////}

        ////void AddReferencedType(Dictionary<XmlQualifiedName, object> referencedTypes, Type type)
        ////{
        ////    if (IsTypeReferenceable(type))
        ////    {
        ////        XmlQualifiedName stableName;
        ////        try
        ////        {
        ////            stableName = this.GetStableName(type);
        ////        }
        ////        catch (InvalidDataContractException)
        ////        {
        ////            // Type not referenceable if we can't get a stable name.
        ////            return;
        ////        }
        ////        catch (InvalidOperationException)
        ////        {
        ////            // Type not referenceable if we can't get a stable name.
        ////            return;
        ////        }

        ////        object value;
        ////        if (referencedTypes.TryGetValue(stableName, out value))
        ////        {
        ////            Type referencedType = value as Type;
        ////            if (referencedType != null)
        ////            {
        ////                if (referencedType != type)
        ////                {
        ////                    referencedTypes.Remove(stableName);
        ////                    List<Type> types = new List<Type>();
        ////                    types.Add(referencedType);
        ////                    types.Add(type);
        ////                    referencedTypes.Add(stableName, types);
        ////                }
        ////            }
        ////            else
        ////            {
        ////                List<Type> types = (List<Type>)value;
        ////                if (!types.Contains(type))
        ////                    types.Add(type);
        ////            }
        ////        }
        ////        else
        ////            referencedTypes.Add(stableName, type);
        ////    }
        ////}
        ////internal bool TryGetReferencedType(XmlQualifiedName stableName, DataContract dataContract, out Type type)
        ////{
        ////    return TryGetReferencedType(stableName, dataContract, false/*useReferencedCollectionTypes*/, out type);
        ////}

        ////internal bool TryGetReferencedCollectionType(XmlQualifiedName stableName, DataContract dataContract, out Type type)
        ////{
        ////    return TryGetReferencedType(stableName, dataContract, true/*useReferencedCollectionTypes*/, out type);
        ////}

        ////bool TryGetReferencedType(XmlQualifiedName stableName, DataContract dataContract, bool useReferencedCollectionTypes, out Type type)
        ////{
        ////    object value;
        ////    Dictionary<XmlQualifiedName, object> referencedTypes = useReferencedCollectionTypes ? GetReferencedCollectionTypes() : GetReferencedTypes();
        ////    if (referencedTypes.TryGetValue(stableName, out value))
        ////    {
        ////        type = value as Type;
        ////        if (type != null)
        ////            return true;
        ////        else
        ////        {
        ////            // Throw ambiguous type match exception
        ////            List<Type> types = (List<Type>)value;
        ////            StringBuilder errorMessage = new StringBuilder();
        ////            bool containsGenericType = false;
        ////            for (int i = 0; i < types.Count; i++)
        ////            {
        ////                Type conflictingType = types[i];
        ////                if (!containsGenericType)
        ////                    containsGenericType = conflictingType.IsGenericTypeDefinition;
        ////                errorMessage.AppendFormat("{0}\"{1}\" ", Environment.NewLine, conflictingType.AssemblyQualifiedName);
        ////                if (dataContract != null)
        ////                {
        ////                    DataContract other = this.GetDataContract(conflictingType);
        ////                    errorMessage.Append(SR.GetString(((other != null && other.Equals(dataContract)) ? SR.ReferencedTypeMatchingMessage : SR.ReferencedTypeNotMatchingMessage)));
        ////                }
        ////            }
        ////            if (containsGenericType)
        ////            {
        ////                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(
        ////                    (useReferencedCollectionTypes ? SR.AmbiguousReferencedCollectionTypes1 : SR.AmbiguousReferencedTypes1),
        ////                    errorMessage.ToString())));
        ////            }
        ////            else
        ////            {
        ////                throw System.Runtime.Serialization.DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(
        ////                    (useReferencedCollectionTypes ? SR.AmbiguousReferencedCollectionTypes3 : SR.AmbiguousReferencedTypes3),
        ////                    XmlConvert.DecodeName(stableName.Name),
        ////                    stableName.Namespace,
        ////                    errorMessage.ToString())));
        ////            }
        ////        }
        ////    }
        ////    type = null;
        ////    return false;
        ////}

        ////static bool IsTypeReferenceable(Type type)
        ////{
        ////    Type itemType;

        ////    try
        ////    {
        ////        return (type.IsSerializable ||
        ////                type.IsDefined(Globals.TypeOfDataContractAttribute, false) ||
        ////                (Globals.TypeOfIXmlSerializable.IsAssignableFrom(type) && !type.IsGenericTypeDefinition) ||
        ////                CollectionDataContract.IsCollection(type, out itemType) ||
        ////                ClassDataContract.IsNonAttributedTypeValidForSerialization(type));
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        // An exception can be thrown in the designer when a project has a runtime binding redirection for a referenced assembly or a reference dependent assembly.
        ////        // Type.IsDefined is known to throw System.IO.FileLoadException.
        ////        // ClassDataContract.IsNonAttributedTypeValidForSerialization is known to throw System.IO.FileNotFoundException.
        ////        // We guard against all non-critical exceptions.
        ////        if (Fx.IsFatal(ex))
        ////        {
        ////            throw;
        ////        }
        ////    }

        ////    return false;
        ////}

        //#region DataContract Methods
        //private static bool IsBuiltInDataContract(object dataContract)
        //{
        //    // TODO: Improve performance
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("IsBuiltInDataContract", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (bool)property.GetValue(dataContract);
        //}

        //private static bool IsISerializable(object dataContract)
        //{
        //    // TODO: Improve performance
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("IsISerializable", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (bool)property.GetValue(dataContract);
        //}

        //private static Type UnderlyingType(object dataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("UnderlyingType", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (Type)property.GetValue(dataContract);
        //}

        //private static XmlQualifiedName StableName(object dataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetProperty("StableName", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (XmlQualifiedName)property.GetValue(dataContract);
        //}

        //private static object ClassDataContractBaseContract(object classDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.ClassDataContract").GetProperty("BaseContract", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (XmlQualifiedName)property.GetValue(classDataContract);
        //}

        //private static IList ClassDataContractMembers(object classDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.ClassDataContract").GetProperty("Members", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (IList)property.GetValue(classDataContract);
        //}

        //private static MemberInfo DataMemberMemberInfo(object dataMember)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataMember").GetProperty("MemberInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (MemberInfo)property.GetValue(dataMember);
        //}

        //private static bool DataMemberIsGetOnlyCollection(object dataMember)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataMember").GetProperty("IsGetOnlyCollection", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (bool)property.GetValue(dataMember);
        //}

        //private static object DataMemberMemberTypeContract(object dataMember)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataMember").GetProperty("MemberTypeContract", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return property.GetValue(dataMember);
        //}

        //private static bool CollectionDataContractIsDictionary(object collectionDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.CollectionDataContract").GetProperty("IsDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (bool)property.GetValue(collectionDataContract);
        //}

        //private static object CollectionDataContractItemContract(object collectionDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.CollectionDataContract").GetProperty("ItemContract", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return property.GetValue(collectionDataContract);
        //}

        //private static Type CollectionDataContractItemType(object collectionDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.CollectionDataContract").GetProperty("ItemType", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (Type)property.GetValue(collectionDataContract);
        //}

        //private static DataContractDictionary CollectionDataContractKnownDataContracts(object collectionDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.CollectionDataContract").GetProperty("KnownDataContracts", BindingFlags.Instance | BindingFlags.NonPublic);
        //    IDictionary dictionary = (IDictionary)property.GetValue(collectionDataContract);
        //    var dcDict = new DataContractDictionary();
        //    foreach(var key in dictionary.Keys)
        //    {
        //        XmlQualifiedName xqnKey = (XmlQualifiedName)key;
        //        dcDict[xqnKey] = dictionary[key];
        //    }

        //    return dcDict;
        //}

        //private static DataContractDictionary XmlDataContractKnownDataContracts(object xmlDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.XmlDataContract").GetProperty("KnownDataContracts", BindingFlags.Instance | BindingFlags.NonPublic);
        //    IDictionary dictionary = (IDictionary)property.GetValue(xmlDataContract);
        //    var dcDict = new DataContractDictionary();
        //    foreach (var key in dictionary.Keys)
        //    {
        //        XmlQualifiedName xqnKey = (XmlQualifiedName)key;
        //        dcDict[xqnKey] = dictionary[key];
        //    }

        //    return dcDict;
        //}

        //private static DataContractDictionary ClassDataContractKnownDataContracts(object classDataContract)
        //{
        //    var property = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.ClassDataContract").GetProperty("KnownDataContracts", BindingFlags.Instance | BindingFlags.NonPublic);
        //    IDictionary dictionary = (IDictionary)property.GetValue(classDataContract);
        //    var dcDict = new DataContractDictionary();
        //    foreach (var key in dictionary.Keys)
        //    {
        //        XmlQualifiedName xqnKey = (XmlQualifiedName)key;
        //        dcDict[xqnKey] = dictionary[key];
        //    }

        //    return dcDict;
        //}

        //private static Type MemberType(MemberInfo memberInfo)
        //{
        //    FieldInfo field = memberInfo as FieldInfo;
        //    if (field != null)
        //        return field.FieldType;
        //    return ((PropertyInfo)memberInfo).PropertyType;
        //}

        //internal static string GetClrTypeFullName(Type type)
        //{
        //    return !type.IsGenericTypeDefinition && type.ContainsGenericParameters ? string.Format(CultureInfo.InvariantCulture, "{0}.{1}", type.Namespace, type.Name) : type.FullName;
        //}

        //internal static int GetId(RuntimeTypeHandle typeHandle)
        //{
        //    var methodInfo = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetMethod("GetId", BindingFlags.Static | BindingFlags.NonPublic);
        //    return (int)methodInfo.Invoke(null, new object[] { typeHandle });
        //}

        //internal static object GetGetOnlyCollectionDataContract(int id, RuntimeTypeHandle typeHandle, Type type, /*SerializationMode*/ int mode)
        //{
        //    var methodInfo = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract").GetMethod("GetGetOnlyCollectionDataContract", BindingFlags.Instance | BindingFlags.NonPublic);
        //    return methodInfo.Invoke(null, new object[] { id, typeHandle, type, mode });
        //}

        //private static bool IsXmlDataContract(object dataContract) => dataContract.GetType().FullName.Equals("System.Runtime.Serialization.XmlDataContract");
        //private static bool IsCollectionDataContract(object dataContract) => dataContract.GetType().FullName.Equals("System.Runtime.Serialization.CollectionDataContract");
        //private static bool IsClassDataContract(object dataContract) => dataContract.GetType().FullName.Equals("System.Runtime.Serialization.ClassDataContract");

        //#endregion
    }
}
