// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CoreWCF.Runtime;

namespace CoreWCF.Xml.Serialization
{
    internal static class XmlMembersMappingExtensions
    {
        private static Func<XmlMapping, object> s_GetScopeDelegate = GetScopeStub;

        private static object GetScopeStub(XmlMapping xmlMapping)
        {
            //var scopeProperty = typeof(XmlMapping).GetProperty("Scope", BindingFlags.NonPublic | BindingFlags.Instance);
            //var scopeGetter = scopeProperty.GetGetMethod();
            //var paramExpression = Expression.Parameter(typeof(XmlMapping), "mm");
            //var emptyExpressionArray = Array.Empty<Expression>();
            //var callExpr = Expression.Call(paramExpression, scopeGetter, emptyExpressionArray);
            //var convertExpr = Expression.Convert(callExpr, typeof(object));
            //var exprArray = new ParameterExpression[1];
            //exprArray[0] = paramExpression;
            //var lambdaExpression = Expression.Lambda<Func<XmlMapping, object>>(convertExpr, exprArray);
            //s_GetScopeDelegate = lambdaExpression.Compile();
            //return s_GetScopeDelegate(xmlMapping);
            s_GetScopeDelegate = GetPropertyStub<XmlMapping>("Scope");
            return s_GetScopeDelegate(xmlMapping);
        }

        internal static Func<T1, object> GetPropertyStub<T1>(string propertyName) where T1 : class
        {
            var propertyInfo = typeof(T1).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            Fx.Assert(propertyInfo != null, $"There must be a non-public instance property called {propertyName} on type {typeof(T1).Name}");
            var propertyGetter = propertyInfo.GetGetMethod();
            var paramExpression = Expression.Parameter(typeof(T1), "prop");
            var emptyExpressionArray = Array.Empty<Expression>();
            var callExpr = Expression.Call(paramExpression, propertyGetter, emptyExpressionArray);
            var convertExpr = Expression.Convert(callExpr, typeof(object));
            var exprArray = new ParameterExpression[1];
            exprArray[0] = paramExpression;
            return Expression.Lambda<Func<T1, object>>(convertExpr, exprArray).Compile();
        }

        internal static Func<object, object> GetPropertyStub(string propertyName, Type objectType)
        {
            var propertyInfo = objectType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            Fx.Assert(propertyInfo != null, $"There must be a non-public instance property called {propertyName} on type {objectType.Name}");
            var propertyGetter = propertyInfo.GetGetMethod();
            var paramExpression = Expression.Parameter(typeof(object), "prop");
            var castExpression = Expression.Convert(paramExpression, objectType);
            var emptyExpressionArray = Array.Empty<Expression>();
            var callExpr = Expression.Call(castExpression, propertyGetter, emptyExpressionArray);
            var convertExpr = Expression.Convert(callExpr, typeof(object));
            var exprArray = new ParameterExpression[1];
            exprArray[0] = paramExpression;
            return Expression.Lambda<Func<object, object>>(convertExpr, exprArray).Compile();
        }

        internal static object GetScope(this XmlMembersMapping xmlMembersMapping)
        {
            return s_GetScopeDelegate(xmlMembersMapping);
        }

        private static Func<XmlMapping, object> s_GetAccessorDelegate = GetAccessorStub;

        private static object GetAccessorStub(XmlMapping xmlMapping)
        {
            s_GetAccessorDelegate = GetPropertyStub<XmlMapping>("Accessor");
            return s_GetAccessorDelegate(xmlMapping);
        }

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

        internal static XmlAttribute CreateXmlAttribute(string prefix, string localName, string namespaceURI, XmlDocument doc)
        {
            return (XmlAttribute)Activator.CreateInstance(typeof(XmlAttribute), prefix, localName, namespaceURI, doc);
        }
    }

    internal class ElementAccessorSurrogate
    {
        private object _accessor;
        private static Func<object, object> s_getMappingDelegate = GetMappingStub;
        private static Func<object, object> s_getNameDelegate = GetNameStub;
        private static Func<object, object> s_getNamespaceDelegate = GetNamespaceStub;
        private static Func<object, object> s_getIsNullableDelegate = GetIsNullableStub;

        private static object GetMappingStub(object accessor)
        {
            var accessorType = accessor.GetType().Assembly.GetType("System.Xml.Serialization.Accessor");
            s_getMappingDelegate = XmlMembersMappingExtensions.GetPropertyStub("Scope", accessorType);
            return s_getMappingDelegate(accessor);
        }

        private static object GetNameStub(object accessor)
        {
            var accessorType = accessor.GetType().Assembly.GetType("System.Xml.Serialization.Accessor");
            s_getNameDelegate = XmlMembersMappingExtensions.GetPropertyStub("Name", accessorType);
            return s_getNameDelegate(accessor);
        }

        private static object GetNamespaceStub(object accessor)
        {
            var accessorType = accessor.GetType().Assembly.GetType("System.Xml.Serialization.Accessor");
            s_getNamespaceDelegate = XmlMembersMappingExtensions.GetPropertyStub("Namespace", accessorType);
            return s_getNamespaceDelegate(accessor);
        }

        private static object GetIsNullableStub(object accessor)
        {
            var elementAccessorType = accessor.GetType().Assembly.GetType("System.Xml.Serialization.ElementAccessor");
            s_getIsNullableDelegate = XmlMembersMappingExtensions.GetPropertyStub("IsNullable", elementAccessorType);
            return s_getIsNullableDelegate(accessor);
        }

        public ElementAccessorSurrogate(object accessor)
        {
            _accessor = accessor;
        }

        public MappingSurrogate Mapping => new MappingSurrogate(s_getMappingDelegate(_accessor));
        public string Name => (string)s_getNameDelegate(_accessor);
        public string Namespace => (string)s_getNamespaceDelegate(_accessor);
        public bool IsNullable => (bool)s_getIsNullableDelegate(_accessor);
    }

    internal class MappingSurrogate
    {
        private static Func<object, object> s_getTypeNameDelegate = GetTypeNameStub;
        private static Func<object, object> s_getNamespaceDelegate = GetNamespaceStub;
        private static Func<object, object> s_getTypeDescDelegate = GetTypeDescStub;
        private static Func<object, object> s_getIncludeInSchemaDelegate = GetIncludeInSchemaStub;

        private static object GetTypeNameStub(object mapping)
        {
            var typeMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.TypeMapping");
            s_getTypeNameDelegate = XmlMembersMappingExtensions.GetPropertyStub("TypeName", typeMappingType);
            return s_getTypeNameDelegate(mapping);
        }

        private static object GetNamespaceStub(object mapping)
        {
            var typeMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.TypeMapping");
            s_getNamespaceDelegate = XmlMembersMappingExtensions.GetPropertyStub("Namespace", typeMappingType);
            return s_getNamespaceDelegate(mapping);
        }

        private static object GetTypeDescStub(object mapping)
        {
            var typeMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.TypeMapping");
            s_getNamespaceDelegate = XmlMembersMappingExtensions.GetPropertyStub("TypeDesc", typeMappingType);
            return s_getNamespaceDelegate(mapping);
        }

        private static object GetIncludeInSchemaStub(object mapping)
        {
            var typeMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.TypeMapping");
            s_getNamespaceDelegate = XmlMembersMappingExtensions.GetPropertyStub("IncludeInSchema", typeMappingType);
            return s_getNamespaceDelegate(mapping);
        }

        protected object _mapping;
        internal object WrappedObject => _mapping;

        private MemberMappingSurrogate[] _memberMappingSurrogateArray;

        public MappingSurrogate(object mapping) => _mapping = mapping;

        internal MemberMappingSurrogate[] Members => GetMemberMappingSurrogateArray();

        internal string TypeName => (string)s_getTypeNameDelegate(_mapping);
        internal string Namespace => (string)s_getNamespaceDelegate(_mapping);
        internal TypeDescSurrogate TypeDesc => new TypeDescSurrogate(s_getTypeDescDelegate(_mapping));
        internal bool IncludeInSchema => (bool)s_getIncludeInSchemaDelegate(_mapping);


        private MemberMappingSurrogate[] GetMemberMappingSurrogateArray()
        {
            if (_memberMappingSurrogateArray == null)
            {
                var membersMappingType = _mapping.GetType().Assembly.GetType("System.Xml.Serialization.MembersMapping");
                var membersPropInfo = membersMappingType.GetProperty("Members", BindingFlags.Instance | BindingFlags.NonPublic);
                var membersArray = (object[])membersPropInfo.GetValue(_mapping);
                _memberMappingSurrogateArray = new MemberMappingSurrogate[membersArray.Length];
                for (int i = 0; i < membersArray.Length; i++)
                {
                    _memberMappingSurrogateArray[i] = new MemberMappingSurrogate(membersArray[i]);
                }
            }

            return _memberMappingSurrogateArray;
        }

        internal bool IsArrayMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.ArrayMapping");
        public bool IsEnumMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.EnumMapping");
        public bool IsPrimitiveMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.PrimitiveMapping");
        public bool IsStructMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.StructMapping");
        public bool IsNullableMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.NullableMapping");
        public bool IsMembersMapping => _mapping.GetType().FullName.Equals("System.Xml.Serialization.MembersMapping");
    }

    internal class TypeDescSurrogate
    {
        private object _typeDesc;

        public TypeDescSurrogate(object typeDesc) => _typeDesc = typeDesc;

        public bool IsXsdType
        {
            get
            {
                var isXsdTypeProperty = _typeDesc.GetType().GetProperty("IsXsdType");
                return (bool)isXsdTypeProperty.GetValue(_typeDesc);
            }
        }

        public bool IsRoot
        {
            get
            {
                var isXsdTypeProperty = _typeDesc.GetType().GetProperty("IsRoot");
                return (bool)isXsdTypeProperty.GetValue(_typeDesc);
            }
        }

        public bool IsAbstract
        {
            get
            {
                var isXsdTypeProperty = _typeDesc.GetType().GetProperty("IsAbstract");
                return (bool)isXsdTypeProperty.GetValue(_typeDesc);
            }
        }

        public XmlSchemaType DataType
        {
            get
            {
                var dataTypeProperty = _typeDesc.GetType().GetProperty("DataType");
                return (XmlSchemaType)dataTypeProperty.GetValue(_typeDesc);
            }
        }

        public string Name
        {
            get
            {
                var dataTypeProperty = _typeDesc.GetType().GetProperty("Name");
                return (string)dataTypeProperty.GetValue(_typeDesc);
            }
        }

        public bool IsValueType
        {
            get
            {
                var isValueTypeProperty = _typeDesc.GetType().GetProperty("IsValueType");
                return (bool)isValueTypeProperty.GetValue(_typeDesc);
            }
        }
    }

    internal class ArrayMappingSurrogate : MappingSurrogate
    {
        private static Func<object, object> s_getNextDelegate = GetNextStub;

        private static object GetNextStub(object arrayMapping)
        {
            var arrayMappingType = arrayMapping.GetType().Assembly.GetType("System.Xml.Serialization.ArrayMapping");
            s_getNextDelegate = XmlMembersMappingExtensions.GetPropertyStub("Next", arrayMappingType);
            return s_getNextDelegate(arrayMapping);
        }

        private ElementAccessorSurrogate[] _elementAccessorSurrogateArray;

        internal ArrayMappingSurrogate(object arrayMapping) : base(arrayMapping) { }

        public ArrayMappingSurrogate Next => GetNext();

        public ElementAccessorSurrogate[] Elements => GetElementAccessorSurrogateArray();

        private ElementAccessorSurrogate[] GetElementAccessorSurrogateArray()
        {
            if (_elementAccessorSurrogateArray == null)
            {
                var accessorMappingType = _mapping.GetType().Assembly.GetType("System.Xml.Serialization.ArrayMapping");
                var elementsPropInfo = accessorMappingType.GetProperty("Elements", BindingFlags.Instance | BindingFlags.NonPublic);
                var elementsArray = (object[])elementsPropInfo.GetValue(_mapping);
                _elementAccessorSurrogateArray = new ElementAccessorSurrogate[elementsArray.Length];
                for (int i = 0; i < elementsArray.Length; i++)
                {
                    _elementAccessorSurrogateArray[i] = new ElementAccessorSurrogate(elementsArray[i]);
                }
            }

            return _elementAccessorSurrogateArray;
        }

        private ArrayMappingSurrogate GetNext()
        {
            var nextObj = s_getNextDelegate(_mapping);
            return nextObj == null ? null : new ArrayMappingSurrogate(nextObj);
        }
    }

    internal class PrimitiveMappingSurrogate : MappingSurrogate
    {
        public PrimitiveMappingSurrogate(object primitiveMapping) : base(primitiveMapping)
        {
        }
    }

    internal class NullableMappingSurrogate : MappingSurrogate
    {
        private static Func<object, object> s_getBaseMappingDelegate = GetBaseMappingStub;

        private static object GetBaseMappingStub(object nullableMapping)
        {
            var nullableMappingType = nullableMapping.GetType().Assembly.GetType("System.Xml.Serialization.NullableMapping");
            s_getBaseMappingDelegate = XmlMembersMappingExtensions.GetPropertyStub("BaseMapping", nullableMappingType);
            return s_getBaseMappingDelegate(nullableMapping);
        }

        public NullableMappingSurrogate(object mapping) : base(mapping) { }

        internal MappingSurrogate BaseMapping => GetBaseMapping();

        private MappingSurrogate GetBaseMapping()
        {
            var baseMappingObj = s_getBaseMappingDelegate(_mapping);
            return baseMappingObj == null ? null : new MappingSurrogate(baseMappingObj);
        }
    }

    internal class StructMappingSurrogate : MappingSurrogate
    {
        private static Func<object, object> s_getDerivedMappingsDelegate = GetDerivedMappingsStub;
        private static Func<object, object> s_getNextDerivedMappingDelegate = GetNextDerivedMappingStub;
        private static Func<object, object> s_getBaseMappingDelegate = GetBaseMappingStub;

        private static object GetDerivedMappingsStub(object structMapping)
        {
            var structMappingType = structMapping.GetType().Assembly.GetType("System.Xml.Serialization.StructMapping");
            s_getDerivedMappingsDelegate = XmlMembersMappingExtensions.GetPropertyStub("DerivedMappings", structMappingType);
            return s_getDerivedMappingsDelegate(structMapping);
        }

        private static object GetNextDerivedMappingStub(object structMapping)
        {
            var structMappingType = structMapping.GetType().Assembly.GetType("System.Xml.Serialization.StructMapping");
            s_getNextDerivedMappingDelegate = XmlMembersMappingExtensions.GetPropertyStub("NextDerivedMapping", structMappingType);
            return s_getNextDerivedMappingDelegate(structMapping);
        }

        private static object GetBaseMappingStub(object structMapping)
        {
            var structMappingType = structMapping.GetType().Assembly.GetType("System.Xml.Serialization.StructMapping");
            s_getBaseMappingDelegate = XmlMembersMappingExtensions.GetPropertyStub("BaseMapping", structMappingType);
            return s_getBaseMappingDelegate(structMapping);
        }

        public StructMappingSurrogate(object mapping) : base(mapping)
        {
        }

        public StructMappingSurrogate DerivedMappings => GetDerivedMappings();

        public StructMappingSurrogate NextDerivedMapping => GetNextDerivedMappings();

        public StructMappingSurrogate BaseMapping => GetBaseMapping();

        private StructMappingSurrogate GetDerivedMappings()
        {
            var derivedMappingObj = s_getDerivedMappingsDelegate(_mapping);
            return derivedMappingObj == null ? null : new StructMappingSurrogate(derivedMappingObj);
        }

        private StructMappingSurrogate GetNextDerivedMappings()
        {
            var derivedMappingObj = s_getNextDerivedMappingDelegate(_mapping);
            return derivedMappingObj == null ? null : new StructMappingSurrogate(derivedMappingObj);
        }

        private StructMappingSurrogate GetBaseMapping()
        {
            var derivedMappingObj = s_getBaseMappingDelegate(_mapping);
            return derivedMappingObj == null ? null : new StructMappingSurrogate(derivedMappingObj);
        }
    }

    internal class EnumMappingSurrogate : PrimitiveMappingSurrogate
    {
        private static Func<object, object> s_getIsFlagsDelegate = GetIsFlagsStub;
        private static object GetIsFlagsStub(object mapping)
        {
            var enumMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.EnumMapping");
            s_getIsFlagsDelegate = XmlMembersMappingExtensions.GetPropertyStub("XmlName", enumMappingType);
            return s_getIsFlagsDelegate(mapping);
        }

        private ConstantMappingSurrogate[] _constantsMappingSurrogateArray;

        public EnumMappingSurrogate(object enumMapping) : base(enumMapping) { }

        public ConstantMappingSurrogate[] Constants => GetConstantsMappingSurrogateArray();

        public bool IsFlags => (bool)s_getIsFlagsDelegate(_mapping);

        private ConstantMappingSurrogate[] GetConstantsMappingSurrogateArray()
        {
            if (_constantsMappingSurrogateArray == null)
            {
                var enumMappingType = _mapping.GetType().Assembly.GetType("System.Xml.Serialization.EnumMapping");
                var constantsPropInfo = enumMappingType.GetProperty("Constants", BindingFlags.Instance | BindingFlags.NonPublic);
                var constantsArray = (object[])constantsPropInfo.GetValue(_mapping);
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
        private static Func<object, object> s_getTypeNameDelegate = GetXmlNameStub;

        private static object GetXmlNameStub(object mapping)
        {
            var constantMappingType = mapping.GetType().Assembly.GetType("System.Xml.Serialization.ConstantMapping");
            s_getTypeNameDelegate = XmlMembersMappingExtensions.GetPropertyStub("XmlName", constantMappingType);
            return s_getTypeNameDelegate(mapping);
        }

        public ConstantMappingSurrogate(object enumMapping) : base(enumMapping) { }

        public string XmlName => (string)s_getTypeNameDelegate(_mapping);
    }


    internal class MemberMappingSurrogate : MappingSurrogate
    {
        private ElementAccessorSurrogate[] _elementAccessorSurrogateArray;

        public MemberMappingSurrogate(object memberMapping) : base(memberMapping) { }

        public SpecifiedAccessor CheckSpecified
        {
            get
            {
                var checkSpecifiedProperty = _mapping.GetType().GetProperty("CheckSpecified");
                return (SpecifiedAccessor)checkSpecifiedProperty.GetValue(_mapping);
            }
        }

        public bool CheckShouldPersist
        {
            get
            {
                var checkShouldPersistProperty = _mapping.GetType().GetProperty("CheckShouldPersist");
                return (bool)checkShouldPersistProperty.GetValue(_mapping);
            }
        }

        internal ElementAccessorSurrogate[] Elements => GetElementAccessorSurrogateArray();

        private ElementAccessorSurrogate[] GetElementAccessorSurrogateArray()
        {
            if (_elementAccessorSurrogateArray == null)
            {
                var accessorMappingType = _mapping.GetType().Assembly.GetType("System.Xml.Serialization.AccessorMapping");
                var elementsPropInfo = accessorMappingType.GetProperty("Elements", BindingFlags.Instance | BindingFlags.NonPublic);
                var elementsArray = (object[])elementsPropInfo.GetValue(_mapping);
                _elementAccessorSurrogateArray = new ElementAccessorSurrogate[elementsArray.Length];
                for (int i = 0; i < elementsArray.Length; i++)
                {
                    _elementAccessorSurrogateArray[i] = new ElementAccessorSurrogate(elementsArray[i]);
                }
            }

            return _elementAccessorSurrogateArray;
        }

        internal enum SpecifiedAccessor
        {
            None,
            ReadOnly,
            ReadWrite,
        }
    }
}
