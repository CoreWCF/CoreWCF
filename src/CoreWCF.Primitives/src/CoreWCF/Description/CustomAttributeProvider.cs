using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace CoreWCF.Description
{
    internal class CustomAttributeProvider
    {
        private enum AttributeProviderType
        {
            Unknown,
            Type,
            MethodInfo,
            MemberInfo,
            ParameterInfo,
        };

        private CustomAttributeProvider(object attrProvider)
        {
            if (attrProvider is Type)
            {
                Type = (Type)attrProvider;
                TypeInfo = Type.GetTypeInfo();
                ProviderType = AttributeProviderType.Type;
            }
            else if (attrProvider is MethodInfo)
            {
                MethodInfo = (MethodInfo)attrProvider;
                ProviderType = AttributeProviderType.MethodInfo;
            }
            else if (attrProvider is MemberInfo)
            {
                MemberInfo = (MemberInfo)attrProvider;
                ProviderType = AttributeProviderType.MemberInfo;
            }
            else if (attrProvider is ParameterInfo)
            {
                ParameterInfo = (ParameterInfo)attrProvider;
                ProviderType = AttributeProviderType.ParameterInfo;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(attrProvider));
            }
        }

        private AttributeProviderType ProviderType { get; set; }
        internal Type Type { get; private set; }
        internal TypeInfo TypeInfo { get; private set; }
        internal MemberInfo MemberInfo { get; private set; }
        internal MethodInfo MethodInfo { get; private set; }
        internal ParameterInfo ParameterInfo { get; private set; }

        public object[] GetCustomAttributes(bool inherit)
        {
            switch (ProviderType)
            {
                case AttributeProviderType.Type:
                    return Type.GetTypeInfo().GetCustomAttributes(inherit).ToArray();
                case AttributeProviderType.MethodInfo:
                    return MethodInfo.GetCustomAttributes(inherit).ToArray();
                case AttributeProviderType.MemberInfo:
                    return MemberInfo.GetCustomAttributes(inherit).ToArray();
                case AttributeProviderType.ParameterInfo:
                    // ParameterInfo.GetCustomAttributes can return null instead of an empty enumerable
                    return ParameterInfo.GetCustomAttributes(inherit)?.ToArray();
            }
            Contract.Assert(false, "This should never execute.");
            throw new InvalidOperationException();
        }

        public object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            var attributes = GetCustomAttributes(inherit);
            if (attributes == null || attributes.Length == 0)
                return attributes;

            object[] result = attributes.Where(attribute => attributeType.IsAssignableFrom(attribute.GetType())).ToArray();

            if (result.Length == 0)
            {
                // Only if we don't find the CoreWCF attribute, look for the S.SM attribute
                if (attributeType == typeof(ServiceContractAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMServiceContractAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelServiceContractAttribute(result[i]);
                    }
                }
                else if (attributeType == typeof(OperationContractAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMOperationContractAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelOperationContractAttribute(result[i]);
                    }
                }
            }

            return result;
        }

        private static ServiceContractAttribute ConvertFromServiceModelServiceContractAttribute(object attr)
        {
            Fx.Assert(attr.GetType().FullName.Equals(ServiceReflector.SMServiceContractAttributeFullName), "Expected attribute of type S.SM.ServiceContractAttribute");
            bool hasProtectionLevel = GetProperty<bool>(attr, "HasProtectionLevel");
            if (hasProtectionLevel)
            {
                // ProtectionLevel isn't supported yet so if it was set on the S.SM.SCA, then we can't do the mapping so throw
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new PlatformNotSupportedException("System.ServiceModel.ServiceContractAttribute.ProtectionLevel"));
            }

            var sca = new ServiceContractAttribute();
            string tmpStr = GetProperty<string>(attr, nameof(ServiceContractAttribute.ConfigurationName));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                sca.ConfigurationName = tmpStr;
            }

            tmpStr = GetProperty<string>(attr, nameof(ServiceContractAttribute.Name));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                sca.Name = tmpStr;
            }

            sca.Namespace = GetProperty<string>(attr, nameof(ServiceContractAttribute.Namespace));
            sca.SessionMode = GetProperty<SessionMode>(attr, nameof(ServiceContractAttribute.SessionMode));
            sca.CallbackContract = GetProperty<Type>(attr, nameof(ServiceContractAttribute.CallbackContract));
            return sca;
        }

        private static OperationContractAttribute ConvertFromServiceModelOperationContractAttribute(object attr)
        {
            Fx.Assert(attr.GetType().FullName.Equals(ServiceReflector.SMOperationContractAttributeFullName), "Expected attribute of type S.SM.OperationContractAttribute");
            bool hasProtectionLevel = GetProperty<bool>(attr, "HasProtectionLevel");
            if (hasProtectionLevel)
            {
                // ProtectionLevel isn't supported yet so if it was set on the S.SM.SCA, then we can't do the mapping so throw
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new PlatformNotSupportedException("System.ServiceModel.OperationContractAttribute.ProtectionLevel"));
            }

            var oca = new OperationContractAttribute();
            string tmpStr = GetProperty<string>(attr, nameof(OperationContractAttribute.Name));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                oca.Name = tmpStr;
            }

            tmpStr = GetProperty<string>(attr, nameof(OperationContractAttribute.Action));
            if (tmpStr != null) // String.Empty apparently is fine
            {
                oca.Action = tmpStr;
            }

            tmpStr = GetProperty<string>(attr, nameof(OperationContractAttribute.ReplyAction));
            if (tmpStr != null) // String.Empty apparently is fine
            {
                oca.ReplyAction = tmpStr;
            }

            oca.AsyncPattern = GetProperty<bool>(attr, nameof(OperationContractAttribute.AsyncPattern));
            oca.IsOneWay = GetProperty<bool>(attr, nameof(OperationContractAttribute.IsOneWay));
            // TODO: IsInitiating and IsTerminating
            return oca;
        }

        private static TProp GetProperty<TProp>(object obj, string propName)
        {
            Fx.Assert(obj != null, "Expected non-null object");
            PropertyInfo propInfo;
            if (typeof(TProp).IsEnum)
            {
                propInfo = obj.GetType().GetProperty(propName);
                Fx.Assert(propInfo != null, $"Could not find property with name {propName} on object of type {obj.GetType().FullName}");
                return (TProp)Enum.ToObject(typeof(TProp), propInfo.GetValue(obj));
            }
            else
            {
                propInfo = obj.GetType().GetProperty(propName, typeof(TProp));
                Fx.Assert(propInfo != null, $"Could not find property with name {propName} of type {typeof(TProp).FullName} on object of type {obj.GetType().FullName}");
                return (TProp)propInfo.GetValue(obj);
            }
        }

        public bool IsDefined(Type attributeType, bool inherit)
        {
            switch (ProviderType)
            {
                case AttributeProviderType.Type:
                    return Type.GetTypeInfo().IsDefined(attributeType, inherit);
                case AttributeProviderType.MethodInfo:
                    return MethodInfo.IsDefined(attributeType, inherit);
                case AttributeProviderType.MemberInfo:
                    return MemberInfo.IsDefined(attributeType, inherit);
                case AttributeProviderType.ParameterInfo:
                    return ParameterInfo.IsDefined(attributeType, inherit);
            }
            Contract.Assert(false, "This should never execute.");
            throw new InvalidOperationException();
        }

        public static implicit operator CustomAttributeProvider(MemberInfo attrProvider)
        {
            return new CustomAttributeProvider(attrProvider);
        }

        public static implicit operator CustomAttributeProvider(MethodInfo attrProvider)
        {
            return new CustomAttributeProvider(attrProvider);
        }

        public static implicit operator CustomAttributeProvider(ParameterInfo attrProvider)
        {
            return new CustomAttributeProvider(attrProvider);
        }

        public static implicit operator CustomAttributeProvider(Type attrProvider)
        {
            return new CustomAttributeProvider(attrProvider);
        }
    }
}