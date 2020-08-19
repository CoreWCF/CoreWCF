using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace CoreWCF.Description
{
    internal static class CustomAttributeProviderExtentions
    {
        private enum AttributeProviderType
        {
            Unknown,
            Type,
            MethodInfo,
            MemberInfo,
            ParameterInfo,
        };

        public static object[] GetCustomAttributesWithCompat(this ICustomAttributeProvider provider, Type attributeType, bool inherit)
        {
            var attributes = provider.GetCustomAttributes(inherit);
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
                else if (attributeType == typeof(MessageContractAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMMessageContractAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelMessageContractAttribute(result[i]);
                    }
                }
                else if (attributeType == typeof(MessageHeaderAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMMessageHeaderAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelMessageHeadertAttribute(result[i]);
                    }
                }
                else if (attributeType == typeof(MessageBodyMemberAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMMessageBodyMemberAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelMessageBodyMemberAttribute(result[i]);
                    }
                }
                else if (attributeType == typeof(MessagePropertyAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMMessagePropertyAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelMessagePropertyAttribute(result[i]);
                    }
                }
                else if (attributeType == typeof(ServiceKnownTypeAttribute))
                {
                    result = attributes.Where(attribute => attribute.GetType().FullName.Equals(ServiceReflector.SMServiceKnownTypeAttributeFullName)).ToArray();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ConvertFromServiceModelServiceKnownTypeAttribute(result[i]);
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


        private static MessageContractAttribute ConvertFromServiceModelMessageContractAttribute(object attr)
        {
            Fx.Assert(attr.GetType().FullName.Equals(ServiceReflector.SMMessageContractAttributeFullName), "Expected attribute of type System.ServiceModel.MessageContract");
            var messageContract = new MessageContractAttribute();
            messageContract.IsWrapped = GetProperty<bool>(attr, nameof(MessageContractAttribute.IsWrapped));
            string tmpStr = GetProperty<string>(attr, nameof(MessageContractAttribute.WrapperName));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageContract.WrapperName = tmpStr;
            }
            tmpStr = GetProperty<string>(attr, nameof(MessageContractAttribute.WrapperNamespace));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageContract.WrapperNamespace = tmpStr;
            }
            return messageContract;
        }

        private static MessageHeaderAttribute ConvertFromServiceModelMessageHeadertAttribute(object attr)
        {
            Fx.Assert(attr.GetType().FullName.Equals(ServiceReflector.SMMessageHeaderAttributeFullName), "Expected attribute of type System.ServiceModel.MessageHeader");

            bool hasProtectionLevel = GetProperty<bool>(attr, "HasProtectionLevel");
            if (hasProtectionLevel)
            {
                // ProtectionLevel isn't supported yet so if it was set on the S.SM.SCA, then we can't do the mapping so throw
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new PlatformNotSupportedException("System.ServiceModel.ServiceContractAttribute.ProtectionLevel"));
            }

            var messageHeader = new MessageHeaderAttribute();

            string tmpStr = GetProperty<string>(attr, nameof(MessageHeaderAttribute.Actor));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageHeader.Actor = tmpStr;
            }
            tmpStr = GetProperty<string>(attr, nameof(MessageHeaderAttribute.Name));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageHeader.Name = tmpStr;
            }
            tmpStr = GetProperty<string>(attr, nameof(MessageHeaderAttribute.Namespace));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageHeader.Namespace = tmpStr;
            }
            messageHeader.MustUnderstand = GetProperty<bool>(attr, nameof(MessageHeaderAttribute.MustUnderstand));
            messageHeader.Relay = GetProperty<bool>(attr, nameof(MessageHeaderAttribute.Relay));
            return messageHeader;
        }

        private static MessageBodyMemberAttribute ConvertFromServiceModelMessageBodyMemberAttribute(object attr)
        {
            Fx.Assert(attr.GetType().FullName.Equals(ServiceReflector.SMMessageBodyMemberAttributeFullName), "Expected attribute of type System.ServiceModel.MessageBodyMember");
            var messageBody = new MessageBodyMemberAttribute();
            string tmpStr = GetProperty<string>(attr, nameof(MessageBodyMemberAttribute.Name));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageBody.Name = tmpStr;
            }
            tmpStr = GetProperty<string>(attr, nameof(MessageBodyMemberAttribute.Namespace));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messageBody.Namespace = tmpStr;
            }
            int order = GetProperty<int>(attr, nameof(MessageBodyMemberAttribute.Order));
            if (order >= 0)
            {
                messageBody.Order = order;
            }
            return messageBody;
        }

        private static ServiceKnownTypeAttribute ConvertFromServiceModelServiceKnownTypeAttribute(object attr)
        {
            //var messsageProperty = new ServiceKnownTypeAttribute();
            Type type = GetProperty<Type>(attr, nameof(ServiceKnownTypeAttribute.Type));
            string methodName = GetProperty<string>(attr, nameof(ServiceKnownTypeAttribute.MethodName));
            Type declaringType = GetProperty<Type>(attr, nameof(ServiceKnownTypeAttribute.DeclaringType));

            if (type != null)
            {
                return new ServiceKnownTypeAttribute(type);
            }
            else
            {
                // This also covers the constructor used with just a method name as declaring type will just be null
                return new ServiceKnownTypeAttribute(methodName, declaringType);
            }
        }

        private static MessagePropertyAttribute ConvertFromServiceModelMessagePropertyAttribute(object attr)
        {
            var messsageProperty = new MessagePropertyAttribute();
            string tmpStr = GetProperty<string>(attr, nameof(MessagePropertyAttribute.Name));
            if (!string.IsNullOrEmpty(tmpStr))
            {
                messsageProperty.Name = tmpStr;
            }

            return messsageProperty;
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
    }
}