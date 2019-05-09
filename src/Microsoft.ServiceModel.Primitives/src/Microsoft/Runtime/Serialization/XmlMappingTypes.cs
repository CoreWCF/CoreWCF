using System;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Xml.Serialization;

namespace Microsoft.Runtime.Serialization
{
    // The classes in this file are used for invoking APIs that exist in System.Xml.XmlSerializer  
    // via reflection. Those APIs are not supposed to be used by application developers, thus we 
    // cannot add them into public contract.
    //internal interface IXmlMappingTypeInnerObject
    //{
    //    IXmlMappingTypeWrapperObject Object { get; }
    //}

    //internal class XmlReflectionImporter
    //{
    //    private static readonly MethodInfo s_importMembersMapping;
    //    private static readonly MethodInfo s_importTypeMapping;
    //    private static readonly MethodInfo s_includeType;

    //    private IXmlMappingTypeWrapperObject _wrapperObject;

    //    public XmlReflectionImporter() : this(null)
    //    {
    //    }

    //    public XmlReflectionImporter(string defaultNs)
    //    {
    //        _wrapperObject =
    //            XmlMappingTypeWrapperFactory.GetWrapper(
    //                XmlMappingTypesHelper.XmlReflectionImporterType, new object[] {defaultNs});
    //    }

    //    static XmlReflectionImporter()
    //    {
    //        IXmlMappingTypeWrapperObject wrapperObject =
    //            XmlMappingTypeWrapperFactory.GetWrapper(
    //                XmlMappingTypesHelper.XmlReflectionImporterType, new object[] {"defaultNs"});

    //        Type[] types;

    //        if (XmlMappingTypesHelper.XmlReflectionMemberType != null)
    //        {
    //            types = new Type[]
    //            {
    //                typeof (string),
    //                typeof (string),
    //                XmlMappingTypesHelper.XmlReflectionMemberType.MakeArrayType(),
    //                typeof (bool),
    //                typeof (bool)
    //            };

    //            s_importMembersMapping = wrapperObject.GetMethod("ImportMembersMapping", types);
    //        }
    //        else
    //        {
    //            // XmlMappingTypesHelper.XmlReflectionMemberType is null only at pure Net Native runtime.
    //            // In that case, we don't really need to set the following method.
    //            s_importMembersMapping = null;
    //        }

    //        types = new Type[] {typeof (Type)};
    //        s_importTypeMapping = wrapperObject.GetMethod("ImportTypeMapping", types);

    //        s_includeType = wrapperObject.GetMethod("IncludeType", types);
    //    }

    //    public XmlMembersMapping ImportMembersMapping(string mappingName, string ns, XmlReflectionMember[] members, bool hasWrapperElement, bool rpc)
    //    {
    //        Array membersObjects =
    //            _wrapperObject.InitializeArray(XmlMappingTypesHelper.XmlReflectionMemberType, members);

    //        object[] parameters = new object[] {mappingName, ns, membersObjects, hasWrapperElement, rpc};
    //        object o = _wrapperObject.CallMethod(s_importMembersMapping, parameters);
    //        return new XmlMembersMapping(o);
    //    }

    //    public XmlTypeMapping ImportTypeMapping(Type type)
    //    {
    //        Type[] types = new Type[] {typeof (Type)};
    //        object[] parameters = new object[] {type};
    //        object o = _wrapperObject.CallMethod(s_importTypeMapping, parameters);
    //        return new XmlTypeMapping(o);
    //    }

    //    public void IncludeType(Type knownType)
    //    {
    //        Type[] types = new Type[] {typeof (Type)};
    //        object[] parameters = new object[] {knownType};
    //        _wrapperObject.CallMethod(s_includeType, parameters);
    //    }
    //}

    //internal abstract class XmlMapping : IXmlMappingTypeInnerObject
    //{
    //    protected IXmlMappingTypeWrapperObject _wrapperObject;

    //    private static MethodInfo s_setKey;
    //    private static bool s_setKeyInitialized;

    //    public XmlMapping(object thisObject)
    //    {
    //        this._wrapperObject = XmlMappingTypeWrapperFactory.GetWrapper(thisObject);
    //    }

    //    public IXmlMappingTypeWrapperObject Object => _wrapperObject;

    //    public string ElementName => (string) _wrapperObject.GetProperty("ElementName");

    //    public string Namespace => (string) _wrapperObject.GetProperty("Namespace");

    //    public string XsdElementName => (string) _wrapperObject.GetProperty("XsdElementName");

    //    private string _key;
    //    internal string Key => _key;

    //    public void SetKey(string key)
    //    {
    //        if (!s_setKeyInitialized)
    //        {
    //            Type[] types = new Type[] {typeof (string)};
    //            s_setKey = _wrapperObject.GetMethod("SetKey", types);
    //            s_setKeyInitialized = true;
    //        }

    //        object[] parameters = new object[] {key};
    //        _wrapperObject.CallMethod(s_setKey, parameters);
    //        _key = key;
    //    }
    //}

    //internal class XmlTypeMapping : XmlMapping
    //{
    //    public XmlTypeMapping(object o)
    //        : base(o)
    //    {
    //    }
    //}

    //internal class XmlMembersMapping : XmlMapping
    //{
    //    public XmlMembersMapping(object o)
    //        : base(o)
    //    {
    //    }

    //    public XmlMemberMapping this[int index]
    //    {
    //        get
    //        {
    //            object o = _wrapperObject.GetIndexerProperty(
    //                XmlMappingTypesHelper.XmlMemberMappingType, index);
    //            return new XmlMemberMapping(o);
    //        }
    //    }
    //}

    //internal class XmlMemberMapping
    //{
    //    private IXmlMappingTypeWrapperObject _wrapperObject;

    //    public XmlMemberMapping(object o)
    //    {
    //        _wrapperObject = XmlMappingTypeWrapperFactory.GetWrapper(o);
    //    }

    //    public string XsdElementName => (string) _wrapperObject.GetProperty("XsdElementName");

    //    public string Namespace => (string) _wrapperObject.GetProperty("Namespace");

    //    public string TypeName => (string) _wrapperObject.GetProperty("TypeName");

    //    public string TypeNamespace => (string) _wrapperObject.GetProperty("TypeNamespace");
    //}

    //internal class XmlReflectionMember : IXmlMappingTypeInnerObject
    //{
    //    private IXmlMappingTypeWrapperObject _wrapperObject;

    //    public XmlReflectionMember()
    //    {
    //        _wrapperObject = XmlMappingTypeWrapperFactory.GetWrapper(XmlMappingTypesHelper.XmlReflectionMemberType);
    //    }

    //    public IXmlMappingTypeWrapperObject Object => _wrapperObject;

    //    public string MemberName
    //    {
    //        get { return (string) _wrapperObject.GetProperty("MemberName"); }

    //        set { _wrapperObject.SetProperty("MemberName", value); }
    //    }

    //    public Type MemberType
    //    {
    //        get { return (Type) _wrapperObject.GetProperty("MemberType"); }

    //        set { _wrapperObject.SetProperty("MemberType", value); }
    //    }

    //    public XmlAttributes XmlAttributes
    //    {
    //        get { return (XmlAttributes) _wrapperObject.GetProperty("XmlAttributes"); }

    //        set { _wrapperObject.SetProperty("XmlAttributes", value); }
    //    }
    //}

    //internal class XmlMappingTypeWrapperFactory
    //{
    //    private static IXmlMappingTypeWrapperFactory s_instance;

    //    private static IXmlMappingTypeWrapperFactory Instance
    //    {
    //        get
    //        {
    //            if (s_instance == null)
    //            {
    //                s_instance = new XmlMappingTypeReflectionWrapperFactory();
    //            }

    //            return s_instance;
    //        }
    //    }

    //    public static IXmlMappingTypeWrapperObject GetWrapper(object innerObject)
    //    {
    //        return Instance.GetWrapper(innerObject);
    //    }

    //    public static IXmlMappingTypeWrapperObject GetWrapper(Type type)
    //    {
    //        return Instance.GetWrapper(type);
    //    }

    //    public static IXmlMappingTypeWrapperObject GetWrapper(Type type, object[] parameters)
    //    {
    //        return Instance.GetWrapper(type, parameters);
    //    }
    //}

    //internal interface IXmlMappingTypeWrapperFactory
    //{
    //    IXmlMappingTypeWrapperObject GetWrapper(object innerObject);

    //    IXmlMappingTypeWrapperObject GetWrapper(Type type);

    //    IXmlMappingTypeWrapperObject GetWrapper(Type type, object[] parameters);
    //}

    //internal class XmlMappingTypeReflectionWrapperFactory : IXmlMappingTypeWrapperFactory
    //{
    //    public IXmlMappingTypeWrapperObject GetWrapper(object innerObject)
    //    {
    //        return new XmlMappingTypeReflectionWrapper(innerObject);
    //    }

    //    public IXmlMappingTypeWrapperObject GetWrapper(Type type)
    //    {
    //        return new XmlMappingTypeReflectionWrapper(type);
    //    }

    //    public IXmlMappingTypeWrapperObject GetWrapper(Type type, object[] parameters)
    //    {
    //        return new XmlMappingTypeReflectionWrapper(type, parameters);
    //    }
    //}

    //internal interface IXmlMappingTypeWrapperObject
    //{
    //    object InnerObject { get; }

    //    Type InnerObjectType { get; }

    //    object GetProperty(string propertyName);

    //    void SetProperty(string propertyName, object value);

    //    object GetIndexerProperty(Type returnType, object parameter);

    //    object CallMethod(MethodInfo method, object[] parameters);

    //    MethodInfo GetMethod(string methodName, Type[] types);

    //    Array InitializeArray(Type type, IXmlMappingTypeInnerObject[] objects);
    //}

    //internal class XmlMappingTypeReflectionWrapper : IXmlMappingTypeWrapperObject
    //{
    //    private object _innerObject;
    //    private Type _innerObjectType;

    //    public XmlMappingTypeReflectionWrapper(object innerObject)
    //    {
    //        _innerObject = innerObject;
    //    }

    //    public XmlMappingTypeReflectionWrapper(Type type)
    //        : this(type, null)
    //    {
    //    }

    //    public XmlMappingTypeReflectionWrapper(Type type, object[] parameters)
    //    {
    //        _innerObject = Activator.CreateInstance(type, parameters);
    //    }

    //    public object InnerObject => _innerObject;

    //    public Type InnerObjectType
    //    {
    //        get
    //        {
    //            if (_innerObjectType == null)
    //            {
    //                _innerObjectType = _innerObject.GetType();
    //            }

    //            return _innerObjectType;
    //        }
    //    }

    //    public object GetProperty(string propertyName)
    //    {
    //        PropertyInfo property = this.InnerObjectType.GetProperty(propertyName);
    //        Contract.Assert(property != null, "Cannot find property: " + propertyName);

    //        MethodInfo method = property.GetGetMethod(false);
    //        Contract.Assert(method != null, "The property does not have Get method: " + propertyName);

    //        return CallMethod(method, new object[0]);
    //    }

    //    public void SetProperty(string propertyName, object value)
    //    {
    //        PropertyInfo property = this.InnerObjectType.GetProperty(propertyName);
    //        Contract.Assert(property != null, "Cannot find property: " + propertyName);

    //        MethodInfo method = property.GetSetMethod(false);
    //        Contract.Assert(method != null, "The property does not have Set method: " + propertyName);

    //        CallMethod(method, new object[] {value});
    //    }

    //    public object GetIndexerProperty(Type returnType, object parameter)
    //    {
    //        MethodInfo method = this.InnerObjectType.GetProperty("Item", returnType).GetGetMethod(false);
    //        Contract.Assert(method != null, "Cannot find the indexer property with return type: " + returnType);

    //        return CallMethod(method, new object[] {parameter});
    //    }

    //    public MethodInfo GetMethod(string methodName, Type[] types)
    //    {
    //        MethodInfo method = this.InnerObjectType.GetMethod(methodName, types);
    //        Contract.Assert(method != null, "Cannot find method: " + methodName);

    //        return method;
    //    }

    //    public Array InitializeArray(Type type, IXmlMappingTypeInnerObject[] objects)
    //    {
    //        return XmlMappingTypesHelper.InitializeArray(type, objects);
    //    }

    //    public object CallMethod(MethodInfo method, object[] parameters)
    //    {
    //        Contract.Assert(method != null, "method");

    //        object o = method.Invoke(this.InnerObject, parameters);

    //        return o;
    //    }
    //}

    //internal class XmlAttributesHelper
    //{
    //    internal static XmlAttributes CreateXmlAttributes(MemberInfo member)
    //    {
    //        return (XmlAttributes) Activator.CreateInstance(typeof (XmlAttributes), new object[] {member});
    //    }
    //}

    //internal static class XmlMappingTypesHelper
    //{
    //    private static string s_xmlReflectionImporterTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlReflectionImporter");

    //    private static string s_xmlMappingTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlMapping");

    //    private static string s_xmlTypeMappingTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlTypeMapping");

    //    private static string s_xmlMembersMappingTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlMembersMapping");

    //    private static string s_xmlMemberMappingTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlMemberMapping");

    //    private static string s_xmlReflectionMemberTypeName =
    //        XmlMappingTypesHelper.GetAssemblyQualifiedTypeName("System.Xml.Serialization.XmlReflectionMember");

    //    private static string s_xmlSerializerAssemblyName;

    //    public static Type XmlReflectionImporterType = Type.GetType(s_xmlReflectionImporterTypeName);
    //    public static Type XmlMappingType = Type.GetType(s_xmlMappingTypeName);
    //    public static Type XmlTypeMappingType = Type.GetType(s_xmlTypeMappingTypeName);
    //    public static Type XmlMembersMappingType = Type.GetType(s_xmlMembersMappingTypeName);
    //    public static Type XmlMemberMappingType = Type.GetType(s_xmlMemberMappingTypeName);
    //    public static Type XmlReflectionMemberType = Type.GetType(s_xmlReflectionMemberTypeName);

    //    public static Array InitializeArray(Type type, IXmlMappingTypeInnerObject[] objects)
    //    {
    //        Array array = Array.CreateInstance(type, objects.Length);
    //        for (int i = 0; i < objects.Length; i++)
    //        {
    //            array.SetValue(objects[i].Object.InnerObject, i);
    //        }

    //        return array;
    //    }

    //    private static string XmlSerializerAssemblyName
    //    {
    //        get
    //        {
    //            if (s_xmlSerializerAssemblyName == null)
    //            {
    //                Type type = typeof (XmlSerializer);
    //                string asmQualifiedName = type.AssemblyQualifiedName;
    //                int index = asmQualifiedName.IndexOf(',') + 1;
    //                s_xmlSerializerAssemblyName = asmQualifiedName.Substring(index);
    //            }

    //            return s_xmlSerializerAssemblyName;
    //        }
    //    }

    //    private static string GetAssemblyQualifiedTypeName(string typeName)
    //    {
    //        return typeName + ", " + XmlSerializerAssemblyName;
    //    }
    //}
}
