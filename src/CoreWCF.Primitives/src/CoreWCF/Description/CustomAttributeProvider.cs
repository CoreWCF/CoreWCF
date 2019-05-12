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

            return attributes.Where(attribute => attributeType.IsAssignableFrom(attribute.GetType())).ToArray();

            // TODO: Revert once issue dotnet/coreclr#8794 is fixed
            //switch (this.ProviderType)
            //{
            //    case AttributeProviderType.Type:
            //        return this.Type.GetTypeInfo().GetCustomAttributes(attributeType, inherit).ToArray();
            //    case AttributeProviderType.MethodInfo:
            //        return this.MethodInfo.GetCustomAttributes(attributeType, inherit).ToArray();
            //    case AttributeProviderType.MemberInfo:
            //        return this.MemberInfo.GetCustomAttributes(attributeType, inherit).ToArray();
            //    case AttributeProviderType.ParameterInfo:
            //        //GetCustomAttributes could return null instead of empty collection for a known System.Relection issue, workaround the issue by explicitly checking the null
            //        IEnumerable<Attribute> customAttributes = null;
            //        customAttributes = this.ParameterInfo.GetCustomAttributes(attributeType, inherit);
            //        return customAttributes == null ? null : customAttributes.ToArray();
            //}
            //Contract.Assert(false, "This should never execute.");
            //throw new InvalidOperationException();
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