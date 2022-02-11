// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        private DataContractEx(object dataContract)
        {
            WrappedDataContract = dataContract ?? throw new ArgumentNullException(nameof(dataContract));
            Fx.Assert(DataContractType.IsAssignableFrom(dataContract.GetType()), "Only types derived from DataContract can be wrapped");
        }

        public object WrappedDataContract { get; }

        public Type UnderlyingType => s_getUnderlyingType(WrappedDataContract);
        public XmlQualifiedName StableName => s_getStableName(WrappedDataContract);
        public XmlDictionaryString TopLevelElementName => s_getTopLevelElementName(WrappedDataContract);
        public XmlDictionaryString TopLevelElementNamespace => s_getTopLevelElementNamespace(WrappedDataContract);
        public bool HasRoot => s_getHasRoot(WrappedDataContract);
        public bool IsXmlDataContract => WrappedDataContract.GetType() == s_xmlDataContractType;
        public bool XmlDataContractIsAnonymous => s_getXmlDataContractIsAnonymous(WrappedDataContract);
        public XmlSchemaType XmlDataContractXsdType => s_getXmlDataContractXsdType(WrappedDataContract);

        internal static Func<Type, DataContractEx> GetDataContract { private set; get; } = GetDataContractStub;

        internal static Type DataContractType => typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.DataContract");
        private static Type s_xmlDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.XmlDataContract");
        private static Type s_enumDataContractType = typeof(DataContractSerializer).Assembly.GetType("System.Runtime.Serialization.EnumDataContract");
        private static Func<object, Type> s_getUnderlyingType = ReflectionHelper.GetPropertyDelegate<Type>(DataContractType, "UnderlyingType");
        private static Func<object, XmlQualifiedName> s_getStableName = ReflectionHelper.GetPropertyDelegate<XmlQualifiedName>(DataContractType, "StableName");
        private static Func<object, XmlDictionaryString> s_getTopLevelElementName = ReflectionHelper.GetPropertyDelegate<XmlDictionaryString>(DataContractType, "TopLevelElementName");
        private static Func<object, XmlDictionaryString> s_getTopLevelElementNamespace = ReflectionHelper.GetPropertyDelegate<XmlDictionaryString>(DataContractType, "TopLevelElementNamespace");
        private static Func<object, bool> s_getHasRoot = ReflectionHelper.GetPropertyDelegate<bool>(DataContractType, "HasRoot");
        private static Func<object, bool> s_getXmlDataContractIsAnonymous = ReflectionHelper.GetPropertyDelegate<bool>(s_xmlDataContractType, "IsAnonymous");
        private static Func<object, XmlSchemaType> s_getXmlDataContractXsdType = ReflectionHelper.GetPropertyDelegate<XmlSchemaType>(s_xmlDataContractType, "XsdType");
        private static Action<object, XmlQualifiedName> s_setEnumBaseContractName = ReflectionHelper.SetPropertyDelegate<XmlQualifiedName>(s_enumDataContractType, "BaseContractName");

        internal static void FixupEnumDataContract(object dataContract)
        {
            if (dataContract.GetType().Equals(s_enumDataContractType))
            {
                var wrapped = new DataContractEx(dataContract);
                var dataType = wrapped.UnderlyingType; // Type that DataContract is for
                Fx.Assert(dataType.IsEnum, "EnumDataContract should only be created for an enum type");
                var underlyingType = Enum.GetUnderlyingType(dataType); // The underlying integer type backing the Enum
                var baseContractName = s_typeToName[underlyingType];
                s_setEnumBaseContractName(dataContract, baseContractName);
            }
        }

        private static DataContractEx GetDataContractStub(Type clrType)
        {
            var methodInfo = DataContractType.GetMethod("GetDataContract", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(Type) }, null);
            var getDataContractDelegate = ReflectionHelper.CreateStaticMethodCallLambda<Type, object>(methodInfo);
            Func<Type, DataContractEx> wrappingDelegate = (Type type) => new DataContractEx(getDataContractDelegate(type));
            GetDataContract = wrappingDelegate;
            return GetDataContract(clrType);
        }
    }
}
