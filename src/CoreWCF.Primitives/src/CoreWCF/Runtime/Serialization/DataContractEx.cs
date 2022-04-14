// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace CoreWCF.Runtime.Serialization
{
    internal class DataContractEx
    {
        protected DataContractEx(object dataContract)
        {
            WrappedDataContract = dataContract ?? throw new ArgumentNullException(nameof(dataContract));
            Fx.Assert(DataContractType.IsAssignableFrom(dataContract.GetType()), "Only types derived from DataContract can be wrapped");
        }

        public object WrappedDataContract { get; }

        public IDictionary KnownDataContracts => s_getKnownDataContracts(WrappedDataContract);
        public Type UnderlyingType => s_getUnderlyingType(WrappedDataContract);
        public XmlQualifiedName StableName => s_getStableName(WrappedDataContract);
        public XmlDictionaryString TopLevelElementName => s_getTopLevelElementName(WrappedDataContract);
        public XmlDictionaryString TopLevelElementNamespace => s_getTopLevelElementNamespace(WrappedDataContract);
        public bool HasRoot => s_getHasRoot(WrappedDataContract);
        public bool IsBuiltInDataContract => s_getIsBuiltInDataContract(WrappedDataContract);
        public bool IsReference => s_getIsReference(WrappedDataContract);
        public bool IsISerializable => s_getIsISerializable(WrappedDataContract);
        public bool IsValueType => s_getIsValueType(WrappedDataContract);

        public sealed override bool Equals(object other)
        {
            Fx.Assert(!(other is DataContractEx), "The unwrapped DataContract should be passed to equals");
            if (WrappedDataContract == other)
                return true;

            return Equals(other, new Dictionary<DataContractPairKey, object>());
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        internal virtual bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            DataContractEx dataContract = Wrap(other);
            if (dataContract != null)
            {
                return StableName.Name == dataContract.StableName.Name && StableName.Namespace == dataContract.StableName.Namespace && IsReference == dataContract.IsReference;
            }

            return false;
        }

        internal bool IsEqualOrChecked(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (WrappedDataContract == other)
                return true;

            if (checkedContracts != null)
            {
                DataContractPairKey contractPairKey = new DataContractPairKey(WrappedDataContract, other);
                if (checkedContracts.ContainsKey(contractPairKey))
                    return true;
                checkedContracts.Add(contractPairKey, null);
            }

            return false;
        }

        internal static string GetClrTypeFullName(Type type)
        {
            return !type.IsGenericTypeDefinition && type.ContainsGenericParameters ? type.Namespace + "." + type.Name : type.FullName!;
        }

        internal static Func<Type, DataContractEx> GetDataContract { private set; get; } = GetDataContractStub;
        internal static Func<int, RuntimeTypeHandle, Type, int, DataContractEx> GetGetOnlyCollectionDataContract { private set; get; } = GetGetOnlyCollectionDataContractStub;
        internal static Func<RuntimeTypeHandle, int> GetId { private set; get; } = GetIdStub;
        internal static Type DataContractType => typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract");
        protected static readonly Type ClassDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.ClassDataContract");
        protected static readonly Type CollectionDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.CollectionDataContract");
        protected static readonly Type EnumDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.EnumDataContract");
        protected static readonly Type PrimitiveDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.PrimitiveDataContract");
        protected static readonly Type XmlDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.XmlDataContract");
        private static readonly Type SerializationModeType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.SerializationMode");

        private static readonly Func<object, Type> s_getUnderlyingType = ReflectionHelper.GetPropertyDelegate<Type>(DataContractType, "UnderlyingType");
        private static readonly Func<object, XmlQualifiedName> s_getStableName = ReflectionHelper.GetPropertyDelegate<XmlQualifiedName>(DataContractType, "StableName");
        private static readonly Func<object, XmlDictionaryString> s_getTopLevelElementName = ReflectionHelper.GetPropertyDelegate<XmlDictionaryString>(DataContractType, "TopLevelElementName");
        private static readonly Func<object, XmlDictionaryString> s_getTopLevelElementNamespace = ReflectionHelper.GetPropertyDelegate<XmlDictionaryString>(DataContractType, "TopLevelElementNamespace");
        private static readonly Func<object, bool> s_getHasRoot = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "HasRoot");
        private static readonly Func<object, bool> s_getIsBuiltInDataContract = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "IsBuiltInDataContract");
        private static readonly Func<object, bool> s_getIsReference = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "IsReference");
        private static readonly Func<object, bool> s_getIsISerializable = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "IsISerializable");
        private static readonly Func<object, bool> s_getIsValueType = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "IsValueType");
        private static readonly Func<object, IDictionary> s_getKnownDataContracts = ReflectionHelper.GetPropertyDelegate<IDictionary>(DataContractType, "KnownDataContracts");

        private static DataContractEx GetDataContractStub(Type clrType)
        {
            var methodInfo = DataContractType.GetMethod("GetDataContract", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(Type) }, null);
            var getDataContractDelegate = ReflectionHelper.CreateStaticMethodCallLambda<Type, object>(methodInfo);
            Func<Type, DataContractEx> wrappingDelegate = (Type type) => Wrap(getDataContractDelegate(type));
            GetDataContract = wrappingDelegate;
            return GetDataContract(clrType);
        }

        private static DataContractEx GetGetOnlyCollectionDataContractStub(int id, RuntimeTypeHandle typeHandle, Type type, int mode)
        {
            var methodInfo = DataContractType.GetMethod("GetGetOnlyCollectionDataContract", BindingFlags.Static | BindingFlags.NonPublic, null,
                new Type[] { typeof(int), typeof(RuntimeTypeHandle), typeof(Type),  }, null);
            var idParam = Expression.Parameter(typeof(int), "id");
            var typeHandleParam = Expression.Parameter(typeof(RuntimeTypeHandle), "typeHandle");
            var typeParam = Expression.Parameter(typeof(Type), "type");
            var intModeParam = Expression.Parameter(typeof(int), "mode");
            var serializationModeParam = Expression.Constant(intModeParam, SerializationModeType); 

            // Passing null as instance expression as static method call
            Expression callExpr = Expression.Call(methodInfo, idParam, typeHandleParam, typeParam, serializationModeParam);
            var lambdaExpr = Expression.Lambda<Func<int, RuntimeTypeHandle, Type, int, object>>(callExpr, idParam, typeHandleParam, typeParam, intModeParam);
            var getGetOnlyCollectionDataContractDelegate = lambdaExpr.Compile();
            Func<int, RuntimeTypeHandle, Type, int, DataContractEx> wrappingDelegate =
                (int idp, RuntimeTypeHandle typeHandlep, Type typep, int modep) =>
                {
                    return Wrap(getGetOnlyCollectionDataContractDelegate(idp, typeHandlep, typep, modep));
                };
            GetGetOnlyCollectionDataContract = wrappingDelegate;
            return GetGetOnlyCollectionDataContract(id, typeHandle, type, mode);
        }

        private static int GetIdStub(RuntimeTypeHandle typeHandle)
        {
            var methodInfo = DataContractType.GetMethod("GetId", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(RuntimeTypeHandle) }, null);
            var getIdDelegate = ReflectionHelper.CreateStaticMethodCallLambda<RuntimeTypeHandle, int>(methodInfo);
            GetId = getIdDelegate;
            return GetId(typeHandle);
        }

        internal static DataContractEx Wrap(object dataContract)
        {
            Fx.Assert(!(dataContract is DataContractEx), "Attempting to wrap a DataContractEx");

            var dataContractType = dataContract.GetType();
            if (ClassDataContractType.Equals(dataContractType))
            {
                return new ClassDataContractEx(dataContract);
            }
            else if (CollectionDataContractType.Equals(dataContractType))
            {
                return new CollectionDataContractEx(dataContract);
            }
            else if (EnumDataContractType.Equals(dataContractType))
            {
                return new EnumDataContractEx(dataContract);
            }
            else if (PrimitiveDataContractType.IsAssignableFrom(dataContractType))
            {
                return new PrimitiveDataContractEx(dataContract);
            }
            else if (XmlDataContractType.Equals(dataContractType))
            {
                return new XmlDataContractEx(dataContract);
            }
            else
            {
                Fx.Assert($"Unrecognized data contract type {dataContractType}");
                return null;
            }
        }
    }

    internal class ClassDataContractEx : DataContractEx
    {
        private List<DataMemberEx> _wrappedMembersList;
        private ClassDataContractEx _baseContract;

        public ClassDataContractEx(object dataContract) : base(dataContract)
        {
            Fx.Assert(ClassDataContractType.Equals(dataContract.GetType()), "Only ClassDataContract can be wrapped");
        }

        public ClassDataContractEx BaseContract
        {
            get
            {
                if (_baseContract == null)
                {
                    var baseContract = s_getBaseContract(WrappedDataContract);
                    if (baseContract != null)
                    {
                        _baseContract = new ClassDataContractEx(baseContract);
                    }
                }

                return _baseContract;
            }
        }

        public List<DataMemberEx> Members
        {
            get
            {
                if (_wrappedMembersList == null)
                {
                    var dataMembersList = s_getMembers(WrappedDataContract);
                    _wrappedMembersList = new List<DataMemberEx>(dataMembersList.Count);
                    foreach(var member in dataMembersList)
                    {
                        _wrappedMembersList.Add(new DataMemberEx(member));
                    }
                }

                return _wrappedMembersList;
            }
        }

        internal override bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (IsEqualOrChecked(other, checkedContracts))
                return true;

            if (base.Equals(other, checkedContracts))
            {
                if (ClassDataContractType.Equals(other?.GetType()))
                {
                    ClassDataContractEx dataContract = new ClassDataContractEx(other);
                    if (IsISerializable)
                    {
                        if (!dataContract.IsISerializable)
                            return false;
                    }
                    else
                    {
                        if (dataContract.IsISerializable)
                            return false;

                        if (Members == null)
                        {
                            if (dataContract.Members != null)
                            {
                                // check that all the datamembers in dataContract.Members are optional
                                if (!IsEveryDataMemberOptional(dataContract.Members))
                                    return false;
                            }
                        }
                        else if (dataContract.Members == null)
                        {
                            // check that all the datamembers in Members are optional
                            if (!IsEveryDataMemberOptional(Members))
                                return false;
                        }
                        else
                        {
                            Dictionary<string, DataMemberEx> membersDictionary = new Dictionary<string, DataMemberEx>(Members.Count);
                            List<DataMemberEx> dataContractMembersList = new List<DataMemberEx>();
                            for (int i = 0; i < Members.Count; i++)
                            {
                                membersDictionary.Add(Members[i].Name, Members[i]);
                            }

                            for (int i = 0; i < dataContract.Members.Count; i++)
                            {
                                // check that all datamembers common to both datacontracts match
                                DataMemberEx dataMember;
                                if (membersDictionary.TryGetValue(dataContract.Members[i].Name, out dataMember))
                                {
                                    if (dataMember.Equals(dataContract.Members[i], checkedContracts))
                                    {
                                        membersDictionary.Remove(dataMember.Name);
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                                // otherwise save the non-matching datamembers for later verification 
                                else
                                {
                                    dataContractMembersList.Add(dataContract.Members[i]);
                                }
                            }

                            // check that datamembers left over from either datacontract are optional
                            if (!IsEveryDataMemberOptional(membersDictionary.Values))
                                return false;
                            if (!IsEveryDataMemberOptional(dataContractMembersList))
                                return false;

                        }
                    }

                    if (BaseContract == null)
                        return (dataContract.BaseContract == null);
                    else if (dataContract.BaseContract == null)
                        return false;
                    else
                        return BaseContract.Equals(dataContract.BaseContract.WrappedDataContract, checkedContracts);
                }
            }

            return false;
        }

        private bool IsEveryDataMemberOptional(IEnumerable<DataMemberEx> dataMembers)
        {
            foreach (var dataMember in dataMembers)
            {
                if (dataMember.IsRequired)
                    return false;
            }
            return true;
        }

        private static readonly Func<object, IList> s_getMembers = ReflectionHelper.GetPropertyDelegate<IList>(ClassDataContractType, "Members");
        private static readonly Func<object, object> s_getBaseContract = ReflectionHelper.GetPropertyDelegate<object>(ClassDataContractType, "BaseContract");
    }

    internal class CollectionDataContractEx : DataContractEx
    {
        private DataContractEx _itemContract;

        public CollectionDataContractEx(object dataContract) : base(dataContract)
        {
            Fx.Assert(CollectionDataContractType.Equals(dataContract.GetType()), "Only CollectionDataContract can be wrapped");
        }

        public DataContractEx ItemContract
        {
            get
            {
                if (_itemContract == null)
                {
                    var itemContract = s_getItemContract(WrappedDataContract);
                    if (itemContract != null)
                    {
                        _itemContract = Wrap(itemContract);
                    }
                }

                return _itemContract;
            }
        }

        public string ItemName => s_getItemName(WrappedDataContract);
        public Type ItemType => s_getItemType(WrappedDataContract);
        public bool IsDictionary => s_getIsDictionary(WrappedDataContract);
        public bool IsItemTypeNullable => s_getIsItemTypeNullable(WrappedDataContract);

        internal override bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (IsEqualOrChecked(other, checkedContracts))
                return true;

            if (base.Equals(other, checkedContracts))
            {
                if (CollectionDataContractType.Equals(other?.GetType()))
                {
                    CollectionDataContractEx dataContract = new CollectionDataContractEx(other);
                    bool thisItemTypeIsNullable = (ItemContract == null) ? false : !ItemContract.IsValueType;
                    bool otherItemTypeIsNullable = (dataContract.ItemContract == null) ? false : !dataContract.ItemContract.IsValueType;
                    return ItemName == dataContract.ItemName &&
                        (IsItemTypeNullable || thisItemTypeIsNullable) == (dataContract.IsItemTypeNullable || otherItemTypeIsNullable) &&
                        ItemContract.Equals(dataContract.ItemContract.WrappedDataContract, checkedContracts);
                }
            }
            return false;
        }

        private static readonly Func<object, object> s_getItemContract = ReflectionHelper.GetPropertyDelegate<object>(CollectionDataContractType, "ItemContract");
        private static readonly Func<object, string> s_getItemName = ReflectionHelper.GetPropertyDelegate<string>(CollectionDataContractType, "ItemName");
        private static readonly Func<object, Type> s_getItemType = ReflectionHelper.GetPropertyDelegate<Type>(CollectionDataContractType, "ItemType");
        private static readonly Func<object, bool> s_getIsDictionary = ReflectionHelper.GetPropertyDelegate<bool>(CollectionDataContractType, "IsDictionary");
        private static readonly Func<object, bool> s_getIsItemTypeNullable = ReflectionHelper.GetPropertyDelegate<bool>(CollectionDataContractType, "IsItemTypeNullable");
    }

    internal class EnumDataContractEx : DataContractEx
    {
        private List<DataMemberEx> _wrappedMembersList;

        public EnumDataContractEx(object dataContract) : base(dataContract)
        {
            Fx.Assert(EnumDataContractType.Equals(dataContract.GetType()), "Only EnumDataContract can be wrapped");
            var dataType = UnderlyingType; // Type that DataContract is for
            Fx.Assert(dataType.IsEnum, "EnumDataContract should only be created for an enum type");
            var underlyingType = Enum.GetUnderlyingType(dataType); // The underlying integer type backing the Enum
            Fx.Assert(s_typeToName.ContainsKey(underlyingType), $"Enum underlying type {underlyingType} is missing from map");
            BaseContractName = s_typeToName[underlyingType];
        }

        public XmlQualifiedName BaseContractName
        {
            set => s_setBaseContractName(WrappedDataContract, value);
        }

        public List<DataMemberEx> Members
        {
            get
            {
                if (_wrappedMembersList == null)
                {
                    var dataMembersList = s_getMembers(WrappedDataContract);
                    _wrappedMembersList = new List<DataMemberEx>(dataMembersList.Count);
                    foreach (var member in dataMembersList)
                    {
                        _wrappedMembersList.Add(new DataMemberEx(member));
                    }
                }

                return _wrappedMembersList;
            }
        }

        public List<long> Values => s_getValues(WrappedDataContract);
        public bool IsFlags => s_getIsFlags(WrappedDataContract);

        internal override bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (IsEqualOrChecked(other, checkedContracts))
                return true;

            if (base.Equals(other, null))
            {
                if (EnumDataContractType.Equals(other?.GetType()))
                {
                    EnumDataContractEx dataContract = new EnumDataContractEx(other);
                    if (Members.Count != dataContract.Members.Count || Values.Count != dataContract.Values.Count)
                        return false;
                    string[] memberNames1 = new string[Members.Count], memberNames2 = new string[Members.Count];
                    for (int i = 0; i < Members.Count; i++)
                    {
                        memberNames1[i] = Members[i].Name;
                        memberNames2[i] = dataContract.Members[i].Name;
                    }
                    Array.Sort(memberNames1);
                    Array.Sort(memberNames2);
                    for (int i = 0; i < Members.Count; i++)
                    {
                        if (memberNames1[i] != memberNames2[i])
                            return false;
                    }

                    return IsFlags == dataContract.IsFlags;
                }
            }
            return false;
        }

        private static readonly Func<object, IList> s_getMembers = ReflectionHelper.GetPropertyDelegate<IList>(EnumDataContractType, "Members");
        private static readonly Func<object, List<long>> s_getValues = ReflectionHelper.GetPropertyDelegate<List<long>>(EnumDataContractType, "Values");
        private static readonly Func<object, bool> s_getIsFlags = ReflectionHelper.GetPropertyDelegate <bool>(EnumDataContractType, "IsFlags");
        private static readonly Action<object, XmlQualifiedName> s_setBaseContractName = ReflectionHelper.SetPropertyDelegate<XmlQualifiedName>(EnumDataContractType, "BaseContractName");

        private static readonly Dictionary<Type, XmlQualifiedName> s_typeToName = new Dictionary<Type, XmlQualifiedName>
        {
            [typeof(sbyte)] = new XmlQualifiedName("byte", XmlSchema.Namespace),
            [typeof(byte)] = new XmlQualifiedName("unsignedByte", XmlSchema.Namespace),
            [typeof(short)] = new XmlQualifiedName("short", XmlSchema.Namespace),
            [typeof(ushort)] = new XmlQualifiedName("unsignedShort", XmlSchema.Namespace),
            [typeof(int)] = new XmlQualifiedName("int", XmlSchema.Namespace),
            [typeof(uint)] = new XmlQualifiedName("unsignedInt", XmlSchema.Namespace),
            [typeof(long)] = new XmlQualifiedName("long", XmlSchema.Namespace),
            [typeof(ulong)] = new XmlQualifiedName("unsignedLong", XmlSchema.Namespace)
        };
    }

    internal class PrimitiveDataContractEx : DataContractEx
    {
        public PrimitiveDataContractEx(object dataContract) : base(dataContract)
        {
            Fx.Assert(PrimitiveDataContractType.IsAssignableFrom(dataContract.GetType()), "Only PrimitiveDataContract or derived type can be wrapped");
        }

        internal override bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (PrimitiveDataContractType.IsAssignableFrom(other?.GetType()))
            {
                Type thisType = WrappedDataContract.GetType();
                Type otherType = other.GetType();
                return (thisType.Equals(otherType) || thisType.IsSubclassOf(otherType) || otherType.IsSubclassOf(thisType));
            }
            return false;
        }
    }

    internal class XmlDataContractEx : DataContractEx
    {
        public XmlDataContractEx(object dataContract) : base(dataContract)
        {
            Fx.Assert(XmlDataContractType.Equals(dataContract.GetType()), "Only XmlDataContract can be wrapped");
        }

        public bool IsAnonymous => s_getIsAnonymous(WrappedDataContract);
        public XmlSchemaType XsdType => s_getXsdType(WrappedDataContract);

        internal override bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (IsEqualOrChecked(other, checkedContracts))
                return true;

            if (XmlDataContractType.Equals(other?.GetType()))
            {
                XmlDataContractEx dataContract = new XmlDataContractEx(other);
                if (HasRoot != dataContract.HasRoot)
                    return false;

                if (IsAnonymous)
                {
                    return dataContract.IsAnonymous;
                }
                else
                {
                    return StableName.Name == dataContract.StableName.Name && StableName.Namespace == dataContract.StableName.Namespace;
                }
            }
            return false;
        }

        private static readonly Func<object, bool> s_getIsAnonymous = ReflectionHelper.GetPropertyDelegate<bool>(XmlDataContractType, "IsAnonymous");
        private static readonly Func<object, XmlSchemaType> s_getXsdType = ReflectionHelper.GetPropertyDelegate<XmlSchemaType>(XmlDataContractType, "XsdType");
    }

    internal class DataMemberEx
    {
        private DataContractEx _memberTypeContract;

        public DataMemberEx(object dataMember)
        {
            WrappedDataMember = dataMember ?? throw new ArgumentNullException(nameof(dataMember));
            Fx.Assert(s_dataMemberType.Equals(dataMember.GetType()), "Only DataMember can be wrapped");
        }

        private object WrappedDataMember { get; }

        public MemberInfo MemberInfo => s_getMemberInfo(WrappedDataMember);
        public Type MemberType => s_getMemberType(WrappedDataMember);
        public bool EmitDefaultValue => s_getEmitDefaultValue(WrappedDataMember);
        public bool IsGetOnlyCollection => s_getIsGetOnlyCollection(WrappedDataMember);
        public bool IsNullable => s_getIsNullable(WrappedDataMember);
        public bool IsRequired => s_getIsRequired(WrappedDataMember);
        public string Name => s_getName(WrappedDataMember);

        public DataContractEx MemberTypeContract
        {
            get
            {
                if (_memberTypeContract == null)
                {
                    var memberTypeContract = s_getMemberTypeContract(WrappedDataMember);
                    if (memberTypeContract != null)
                    {
                        _memberTypeContract = DataContractEx.Wrap(memberTypeContract);
                    }
                }

                return _memberTypeContract;
            }
        }

        internal bool Equals(object other, Dictionary<DataContractPairKey, object> checkedContracts)
        {
            if (this == other)
                return true;

            DataMemberEx dataMember = other as DataMemberEx;
            if (dataMember != null)
            {
                // Note: comparison does not use Order hint since it influences element order but does not specify exact order
                bool thisIsNullable = (MemberTypeContract == null) ? false : !MemberTypeContract.IsValueType;
                bool dataMemberIsNullable = (dataMember.MemberTypeContract == null) ? false : !dataMember.MemberTypeContract.IsValueType;
                return Name == dataMember.Name
                       && (IsNullable || thisIsNullable) == (dataMember.IsNullable || dataMemberIsNullable)
                       && IsRequired == dataMember.IsRequired
                       && EmitDefaultValue == dataMember.EmitDefaultValue
                       && MemberTypeContract.Equals(dataMember.MemberTypeContract.WrappedDataContract, checkedContracts);
            }
            return false;
        }

        private static readonly Type s_dataMemberType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataMember");

        private static readonly Func<object, MemberInfo> s_getMemberInfo = ReflectionHelper.GetPropertyDelegate<MemberInfo>(s_dataMemberType, "MemberInfo");
        private static readonly Func<object, Type> s_getMemberType = ReflectionHelper.GetPropertyDelegate<Type>(s_dataMemberType, "MemberType");
        private static readonly Func<object, bool> s_getEmitDefaultValue = ReflectionHelper.GetPropertyDelegate<bool>(s_dataMemberType, "EmitDefaultValue");
        private static readonly Func<object, bool> s_getIsGetOnlyCollection = ReflectionHelper.GetPropertyDelegate<bool>(s_dataMemberType, "IsGetOnlyCollection");
        private static readonly Func<object, bool> s_getIsRequired = ReflectionHelper.GetPropertyDelegate<bool>(s_dataMemberType, "IsRequired");
        private static readonly Func<object, bool> s_getIsNullable = ReflectionHelper.GetPropertyDelegate<bool>(s_dataMemberType, "IsNullable");
        private static readonly Func<object, string> s_getName = ReflectionHelper.GetPropertyDelegate<string>(s_dataMemberType, "Name");
        private static readonly Func<object, object> s_getMemberTypeContract = ReflectionHelper.GetPropertyDelegate<object>(s_dataMemberType, "MemberTypeContract");
    }

    internal class DataContractPairKey
    {
        private readonly object _object1;
        private readonly object _object2;

        public DataContractPairKey(object object1, object object2)
        {
            _object1 = object1;
            _object2 = object2;
        }

        public override bool Equals(object other)
        {
            DataContractPairKey otherKey = other as DataContractPairKey;
            if (otherKey == null)
                return false;
            return (otherKey._object1 == _object1 && otherKey._object2 == _object2) || (otherKey._object1 == _object2 && otherKey._object2 == _object1);
        }

        public override int GetHashCode()
        {
            return _object1.GetHashCode() ^ _object2.GetHashCode();
        }
    }
}
