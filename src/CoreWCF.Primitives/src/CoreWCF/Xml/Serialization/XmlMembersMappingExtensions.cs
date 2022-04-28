// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.Xml.Serialization
{
    internal static class XmlMembersMappingExtensions
    {
        private static Func<XmlMapping, object> s_GetScopeDelegate = ReflectionHelper.GetPropertyDelegate<XmlMapping, object>("Scope");

        internal static object GetScope(this XmlMembersMapping xmlMembersMapping)
        {
            return s_GetScopeDelegate(xmlMembersMapping);
        }

        private static Func<XmlMapping, object> s_GetAccessorDelegate = ReflectionHelper.GetPropertyDelegate<XmlMapping, object>("Accessor");

        internal static ElementAccessorSurrogate GetAccessor(this XmlMembersMapping xmlMembersMapping)
        {
            var accessor = s_GetAccessorDelegate(xmlMembersMapping);
            return new ElementAccessorSurrogate(accessor);
        }

        internal static ArrayMappingSurrogate AsArrayMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is ArrayMappingSurrogate) return mappingSurrogate as ArrayMappingSurrogate;
            return new ArrayMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static EnumMappingSurrogate AsEnumMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is EnumMappingSurrogate) return mappingSurrogate as EnumMappingSurrogate;
            return new EnumMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static PrimitiveMappingSurrogate AsPrimitiveMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is PrimitiveMappingSurrogate) return mappingSurrogate as PrimitiveMappingSurrogate;
            return new PrimitiveMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static StructMappingSurrogate AsStructMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is StructMappingSurrogate) return mappingSurrogate as StructMappingSurrogate;
            return new StructMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static NullableMappingSurrogate AsNullableMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is NullableMappingSurrogate) return mappingSurrogate as NullableMappingSurrogate;
            return new NullableMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static MembersMappingSurrogate AsMembersMappingSurrogate(this MappingSurrogate mappingSurrogate)
        {
            if (mappingSurrogate is MembersMappingSurrogate) return mappingSurrogate as MembersMappingSurrogate;
            return new MembersMappingSurrogate(mappingSurrogate.WrappedObject);
        }

        internal static XmlAttribute CreateXmlAttribute(string prefix, string localName, string namespaceURI, XmlDocument doc)
        {
            return new XmlAttributeImpl(prefix, localName, namespaceURI, doc);
        }

        private class XmlAttributeImpl : XmlAttribute
        {
            public XmlAttributeImpl(string prefix, string localName, string namespaceURI, XmlDocument doc) : base(prefix, localName, namespaceURI, doc) { }
        }
    }

    internal class AccessorSurrogate
    {
        internal static Type AccessorType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.Accessor");
        private static Func<object, object> s_getMappingDelegate = ReflectionHelper.GetPropertyDelegate<object>(AccessorType, "Mapping");
        private static Func<object, string> s_getNameDelegate = ReflectionHelper.GetPropertyDelegate<string>(AccessorType, "Name");
        private static Func<object, string> s_getNamespaceDelegate = ReflectionHelper.GetPropertyDelegate<string>(AccessorType, "Namespace");

        internal object WrappedAccessor { get; }

        public AccessorSurrogate(object accessor) => WrappedAccessor = accessor;

        public MappingSurrogate Mapping => new MappingSurrogate(s_getMappingDelegate(WrappedAccessor));
        public string Name => s_getNameDelegate(WrappedAccessor);
        public string Namespace => s_getNamespaceDelegate(WrappedAccessor);
    }

    internal class ElementAccessorSurrogate : AccessorSurrogate
    {
        internal static Type ElementAccessorType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.ElementAccessor");
        private static Func<object, bool> s_getIsNullableDelegate = ReflectionHelper.GetPropertyDelegate<bool>(ElementAccessorType, "IsNullable");

        public ElementAccessorSurrogate(object accessor) : base(accessor) { }

        public bool IsNullable => (bool)s_getIsNullableDelegate(WrappedAccessor);
    }

    internal class MappingSurrogate
    {
        internal object WrappedObject { get; }

        public MappingSurrogate(object mapping) => WrappedObject = mapping;

        internal bool IsArrayMapping => WrappedObject.GetType() == ArrayMappingSurrogate.ArrayMappingType;
        public bool IsEnumMapping => WrappedObject.GetType() == EnumMappingSurrogate.EnumMappingType;
        public bool IsPrimitiveMapping => WrappedObject.GetType() == PrimitiveMappingSurrogate.PrimitiveMappingType;
        public bool IsStructMapping => WrappedObject.GetType() == StructMappingSurrogate.StructMappingType;
        public bool IsNullableMapping => WrappedObject.GetType() == NullableMappingSurrogate.NullableMappingType;
        public bool IsMembersMapping => WrappedObject.GetType() == MembersMappingSurrogate.MembersMappingType;

        public override bool Equals(object obj)
        {
            if (obj is MappingSurrogate other)
            {
                return ReferenceEquals(WrappedObject, other.WrappedObject);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(WrappedObject);
        }
    }

    internal class TypeMappingSurrogate : MappingSurrogate
    {
        internal static Type TypeMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.TypeMapping");
        private static Func<object, string> s_getTypeNameDelegate = ReflectionHelper.GetPropertyDelegate<string>(TypeMappingType, "TypeName");
        private static Func<object, string> s_getNamespaceDelegate = ReflectionHelper.GetPropertyDelegate<string>(TypeMappingType, "Namespace");
        private static Func<object, object> s_getTypeDescDelegate = ReflectionHelper.GetPropertyDelegate<object>(TypeMappingType, "TypeDesc");
        private static Func<object, bool> s_getIncludeInSchemaDelegate = ReflectionHelper.GetPropertyDelegate<bool>(TypeMappingType, "IncludeInSchema");

        public TypeMappingSurrogate(object mapping) : base(mapping) { }
        internal string TypeName => s_getTypeNameDelegate(WrappedObject);
        internal string Namespace => s_getNamespaceDelegate(WrappedObject);
        internal TypeDescSurrogate TypeDesc => new TypeDescSurrogate(s_getTypeDescDelegate(WrappedObject));
        internal bool IncludeInSchema => s_getIncludeInSchemaDelegate(WrappedObject);
    }

    internal class ArrayMappingSurrogate : TypeMappingSurrogate
    {
        internal static Type ArrayMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.ArrayMapping");
        private static Func<object, object> s_getNextDelegate = ReflectionHelper.GetPropertyDelegate<object>(ArrayMappingType, "Next");
        private static Func<object, object[]> s_getElementsDelegate = ReflectionHelper.GetPropertyDelegate<object[]>(ArrayMappingType, "Elements");

        private ElementAccessorSurrogate[] _elementAccessorSurrogateArray;

        internal ArrayMappingSurrogate(object arrayMapping) : base(arrayMapping) { }

        public ArrayMappingSurrogate Next
        {
            get
            {
                var nextObj = s_getNextDelegate(WrappedObject);
                return nextObj == null ? null : new ArrayMappingSurrogate(nextObj);
            }
        }

        public ElementAccessorSurrogate[] Elements => GetElementAccessorSurrogateArray();

        private ElementAccessorSurrogate[] GetElementAccessorSurrogateArray()
        {
            if (_elementAccessorSurrogateArray == null)
            {
                var elementsArray = s_getElementsDelegate(WrappedObject);
                _elementAccessorSurrogateArray = new ElementAccessorSurrogate[elementsArray.Length];
                for (int i = 0; i < elementsArray.Length; i++)
                {
                    _elementAccessorSurrogateArray[i] = new ElementAccessorSurrogate(elementsArray[i]);
                }
            }

            return _elementAccessorSurrogateArray;
        }
    }

    internal class PrimitiveMappingSurrogate : TypeMappingSurrogate
    {
        internal static Type PrimitiveMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.PrimitiveMapping");

        public PrimitiveMappingSurrogate(object primitiveMapping) : base(primitiveMapping)
        {
        }
    }

    internal class NullableMappingSurrogate : TypeMappingSurrogate
    {
        internal static Type NullableMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.NullableMapping");
        private static Func<object, object> s_getBaseMappingDelegate = ReflectionHelper.GetPropertyDelegate<object>(NullableMappingType, "BaseMapping");

        public NullableMappingSurrogate(object mapping) : base(mapping) { }

        internal MappingSurrogate BaseMapping
        {
            get
            {
                var baseMappingObj = s_getBaseMappingDelegate(WrappedObject);
                return baseMappingObj == null ? null : new MappingSurrogate(baseMappingObj);
            }
        }
    }

    internal class StructMappingSurrogate : TypeMappingSurrogate
    {
        internal static Type StructMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.StructMapping");
        private static Func<object, object> s_getDerivedMappingsDelegate = ReflectionHelper.GetPropertyDelegate<object>(StructMappingType, "DerivedMappings");
        private static Func<object, object> s_getNextDerivedMappingDelegate = ReflectionHelper.GetPropertyDelegate<object>(StructMappingType, "NextDerivedMapping");
        private static Func<object, object> s_getBaseMappingDelegate = ReflectionHelper.GetPropertyDelegate<object>(StructMappingType, "BaseMapping");
        private static Func<object, object[]> s_getMembersDelegate = ReflectionHelper.GetPropertyDelegate<object[]>(StructMappingType, "Members");

        private MemberMappingSurrogate[] _memberMappingSurrogateArray;

        public StructMappingSurrogate(object mapping) : base(mapping)
        {
        }

        internal MemberMappingSurrogate[] Members => GetMemberMappingSurrogateArray();

        private MemberMappingSurrogate[] GetMemberMappingSurrogateArray()
        {
            if (_memberMappingSurrogateArray == null)
            {
                var membersArray = s_getMembersDelegate(WrappedObject);
                _memberMappingSurrogateArray = new MemberMappingSurrogate[membersArray.Length];
                for (int i = 0; i < membersArray.Length; i++)
                {
                    _memberMappingSurrogateArray[i] = new MemberMappingSurrogate(membersArray[i]);
                }
            }

            return _memberMappingSurrogateArray;
        }

        public StructMappingSurrogate DerivedMappings
        {
            get
            {
                var derivedMappingObj = s_getDerivedMappingsDelegate(WrappedObject);
                return derivedMappingObj == null ? null : new StructMappingSurrogate(derivedMappingObj);
            }
        }

        public StructMappingSurrogate NextDerivedMapping
        {
            get
            {
                var nextDerivedMappingObj = s_getNextDerivedMappingDelegate(WrappedObject);
                return nextDerivedMappingObj == null ? null : new StructMappingSurrogate(nextDerivedMappingObj);
            }
        }

        public StructMappingSurrogate BaseMapping
        {
            get
            {
                var derivedMappingObj = s_getBaseMappingDelegate(WrappedObject);
                return derivedMappingObj == null ? null : new StructMappingSurrogate(derivedMappingObj);
            }
        }
    }

    internal class EnumMappingSurrogate : PrimitiveMappingSurrogate
    {
        internal static Type EnumMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.EnumMapping");
        private static Func<object, bool> s_getIsFlagsDelegate = ReflectionHelper.GetPropertyDelegate<bool>(EnumMappingType, "IsFlags");
        private static Func<object, object[]> s_getConstantsDelegate = ReflectionHelper.GetPropertyDelegate<object[]>(EnumMappingType, "Constants");

        private ConstantMappingSurrogate[] _constantsMappingSurrogateArray;

        public EnumMappingSurrogate(object enumMapping) : base(enumMapping) { }

        public ConstantMappingSurrogate[] Constants => GetConstantsMappingSurrogateArray();

        public bool IsFlags => s_getIsFlagsDelegate(WrappedObject);

        private ConstantMappingSurrogate[] GetConstantsMappingSurrogateArray()
        {
            if (_constantsMappingSurrogateArray == null)
            {
                var constantsArray = s_getConstantsDelegate(WrappedObject);
                _constantsMappingSurrogateArray = new ConstantMappingSurrogate[constantsArray.Length];
                for (int i = 0; i < constantsArray.Length; i++)
                {
                    _constantsMappingSurrogateArray[i] = new ConstantMappingSurrogate(constantsArray[i]);
                }
            }

            return _constantsMappingSurrogateArray;
        }
    }

    internal class ConstantMappingSurrogate : MappingSurrogate
    {
        internal static Type ConstantMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.ConstantMapping");
        private static Func<object, string> s_getTypeNameDelegate = ReflectionHelper.GetPropertyDelegate<string>(ConstantMappingType, "XmlName");

        public ConstantMappingSurrogate(object mapping) : base(mapping) { }

        public string XmlName => s_getTypeNameDelegate(WrappedObject);
    }

    internal class AccessorMappingSurrogate : MappingSurrogate
    {
        internal static Type AccessorMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.AccessorMapping");
        private static Func<object, object[]> s_getElementsDelegate = ReflectionHelper.GetPropertyDelegate<object[]>(AccessorMappingType, "Elements");
        private static Func<object, object> s_getTypeDescDelegate = ReflectionHelper.GetPropertyDelegate<object>(AccessorMappingType, "TypeDesc");

        private ElementAccessorSurrogate[] _elementAccessorSurrogateArray;

        public AccessorMappingSurrogate(object mapping) : base(mapping) { }

        internal TypeDescSurrogate TypeDesc => new TypeDescSurrogate(s_getTypeDescDelegate(WrappedObject));

        internal ElementAccessorSurrogate[] Elements => GetElementAccessorSurrogateArray();

        private ElementAccessorSurrogate[] GetElementAccessorSurrogateArray()
        {
            if (_elementAccessorSurrogateArray == null)
            {
                var elementsArray = s_getElementsDelegate(WrappedObject);
                _elementAccessorSurrogateArray = new ElementAccessorSurrogate[elementsArray.Length];
                for (int i = 0; i < elementsArray.Length; i++)
                {
                    _elementAccessorSurrogateArray[i] = new ElementAccessorSurrogate(elementsArray[i]);
                }
            }

            return _elementAccessorSurrogateArray;
        }
    }

    internal class MemberMappingSurrogate : AccessorMappingSurrogate
    {
        internal static Type MemberMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.MemberMapping");
        private static Func<object, int> s_getCheckSpecifiedDelegate = ReflectionHelper.GetPropertyDelegate<int>(MemberMappingType, "CheckSpecified");
        private static Func<object, bool> s_getCheckShouldPersistDelegate = ReflectionHelper.GetPropertyDelegate<bool>(MemberMappingType, "CheckShouldPersist");

        public MemberMappingSurrogate(object memberMapping) : base(memberMapping) { }

        public SpecifiedAccessor CheckSpecified => (SpecifiedAccessor)s_getCheckSpecifiedDelegate(WrappedObject);

        public bool CheckShouldPersist => s_getCheckShouldPersistDelegate(WrappedObject);

        internal enum SpecifiedAccessor
        {
            None,
            ReadOnly,
            ReadWrite,
        }
    }

    internal class MembersMappingSurrogate : TypeMappingSurrogate
    {
        internal static Type MembersMappingType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.MembersMapping");
        private static Func<object, object[]> s_getMembersDelegate = ReflectionHelper.GetPropertyDelegate<object[]>(MembersMappingType, "Members");

        public MembersMappingSurrogate(object mapping) : base(mapping) { }

        private MemberMappingSurrogate[] _memberMappingSurrogateArray;
        internal virtual MemberMappingSurrogate[] Members => GetMemberMappingSurrogateArray();
        private MemberMappingSurrogate[] GetMemberMappingSurrogateArray()
        {
            if (_memberMappingSurrogateArray == null)
            {
                var membersArray = s_getMembersDelegate(WrappedObject);
                _memberMappingSurrogateArray = new MemberMappingSurrogate[membersArray.Length];
                for (int i = 0; i < membersArray.Length; i++)
                {
                    _memberMappingSurrogateArray[i] = new MemberMappingSurrogate(membersArray[i]);
                }
            }

            return _memberMappingSurrogateArray;
        }
    }

    internal class TypeDescSurrogate
    {
        internal static Type TypeDescType { get; } = typeof(XmlElementAttribute).Assembly.GetType("System.Xml.Serialization.TypeDesc");
        private static Func<object, bool> s_getIsXsdTypeDelegate = ReflectionHelper.GetPropertyDelegate<bool>(TypeDescType, "IsXsdType");
        private static Func<object, bool> s_getIsRootDelegate = ReflectionHelper.GetPropertyDelegate<bool>(TypeDescType, "IsRoot");
        private static Func<object, bool> s_getIsAbstractDelegate = ReflectionHelper.GetPropertyDelegate<bool>(TypeDescType, "IsAbstract");
        private static Func<object, XmlSchemaType> s_getDataTypeDelegate = ReflectionHelper.GetPropertyDelegate<XmlSchemaType>(TypeDescType, "DataType");
        private static Func<object, string> s_getNameDelegate = ReflectionHelper.GetPropertyDelegate<string>(TypeDescType, "Name");
        private static Func<object, bool> s_getIsValueTypeDelegate = ReflectionHelper.GetPropertyDelegate<bool>(TypeDescType, "IsValueType");

        private object _typeDesc;

        public TypeDescSurrogate(object typeDesc) => _typeDesc = typeDesc;

        public bool IsXsdType => s_getIsXsdTypeDelegate(_typeDesc);
        public bool IsRoot => s_getIsRootDelegate(_typeDesc);
        public bool IsAbstract => s_getIsAbstractDelegate(_typeDesc);
        public XmlSchemaType DataType => s_getDataTypeDelegate(_typeDesc);
        public string Name => s_getNameDelegate(_typeDesc);
        public bool IsValueType => s_getIsValueTypeDelegate(_typeDesc);
    }


}
