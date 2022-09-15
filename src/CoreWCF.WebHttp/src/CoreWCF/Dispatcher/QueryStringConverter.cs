// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    public class QueryStringConverter
    {
        private readonly Hashtable _defaultSupportedQueryStringTypes;

        // the cache does not have a quota since it is per endpoint and is
        // bounded by the number of types in the contract at the endpoint
        private readonly Hashtable _typeConverterCache;

        public QueryStringConverter()
        {
            _defaultSupportedQueryStringTypes = new Hashtable();
            _defaultSupportedQueryStringTypes.Add(typeof(byte), null);
            _defaultSupportedQueryStringTypes.Add(typeof(sbyte), null);
            _defaultSupportedQueryStringTypes.Add(typeof(short), null);
            _defaultSupportedQueryStringTypes.Add(typeof(int), null);
            _defaultSupportedQueryStringTypes.Add(typeof(long), null);
            _defaultSupportedQueryStringTypes.Add(typeof(ushort), null);
            _defaultSupportedQueryStringTypes.Add(typeof(uint), null);
            _defaultSupportedQueryStringTypes.Add(typeof(ulong), null);
            _defaultSupportedQueryStringTypes.Add(typeof(float), null);
            _defaultSupportedQueryStringTypes.Add(typeof(double), null);
            _defaultSupportedQueryStringTypes.Add(typeof(bool), null);
            _defaultSupportedQueryStringTypes.Add(typeof(char), null);
            _defaultSupportedQueryStringTypes.Add(typeof(decimal), null);
            _defaultSupportedQueryStringTypes.Add(typeof(string), null);
            _defaultSupportedQueryStringTypes.Add(typeof(object), null);
            _defaultSupportedQueryStringTypes.Add(typeof(DateTime), null);
            _defaultSupportedQueryStringTypes.Add(typeof(TimeSpan), null);
            _defaultSupportedQueryStringTypes.Add(typeof(byte[]), null);
            _defaultSupportedQueryStringTypes.Add(typeof(Guid), null);
            _defaultSupportedQueryStringTypes.Add(typeof(Uri), null);
            _defaultSupportedQueryStringTypes.Add(typeof(DateTimeOffset), null);
            _typeConverterCache = new Hashtable();
        }

        public virtual bool CanConvert(Type type)
        {
            if (_defaultSupportedQueryStringTypes.ContainsKey(type))
            {
                return true;
            }

            // otherwise check if its an enum
            if (typeof(Enum).IsAssignableFrom(type))
            {
                return true;
            }

            // check if there's a typeconverter defined on the type
            return (GetStringConverter(type) != null);
        }

        public virtual object ConvertStringToValue(string parameter, Type parameterType)
        {
            if (parameterType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameterType));
            }

            switch (Type.GetTypeCode(parameterType))
            {
                case TypeCode.Byte:
                    return parameter == null ? default : XmlConvert.ToByte(parameter);
                case TypeCode.SByte:
                    return parameter == null ? default : XmlConvert.ToSByte(parameter);
                case TypeCode.Int16:
                    return parameter == null ? default : XmlConvert.ToInt16(parameter);
                case TypeCode.Int32:
                    {
                        if (typeof(Enum).IsAssignableFrom(parameterType))
                        {
                            return Enum.Parse(parameterType, parameter, true);
                        }
                        else
                        {
                            return parameter == null ? default : XmlConvert.ToInt32(parameter);
                        }
                    }
                case TypeCode.Int64:
                    return parameter == null ? default : XmlConvert.ToInt64(parameter);
                case TypeCode.UInt16:
                    return parameter == null ? default : XmlConvert.ToUInt16(parameter);
                case TypeCode.UInt32:
                    return parameter == null ? default : XmlConvert.ToUInt32(parameter);
                case TypeCode.UInt64:
                    return parameter == null ? default : XmlConvert.ToUInt64(parameter);
                case TypeCode.Single:
                    return parameter == null ? default : XmlConvert.ToSingle(parameter);
                case TypeCode.Double:
                    return parameter == null ? default : XmlConvert.ToDouble(parameter);
                case TypeCode.Char:
                    return parameter == null ? default : XmlConvert.ToChar(parameter);
                case TypeCode.Decimal:
                    return parameter == null ? default : XmlConvert.ToDecimal(parameter);
                case TypeCode.Boolean:
                    return parameter == null ? default : Convert.ToBoolean(parameter, CultureInfo.InvariantCulture);
                case TypeCode.String:
                    return parameter;
                case TypeCode.DateTime:
                    return parameter == null ? default : DateTime.Parse(parameter, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                default:
                    {
                        if (parameterType == typeof(TimeSpan))
                        {
                            // support the XML as well as default way of representing timespans
                            TimeSpan result;
                            if (!TimeSpan.TryParse(parameter, out result))
                            {
                                result = parameter == null ? default : XmlConvert.ToTimeSpan(parameter);
                            }
                            return result;
                        }
                        else if (parameterType == typeof(Guid))
                        {
                            return parameter == null ? default : XmlConvert.ToGuid(parameter);
                        }
                        else if (parameterType == typeof(DateTimeOffset))
                        {
                            return (parameter == null) ? default : DateTimeOffset.Parse(parameter, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
                        }
                        else if (parameterType == typeof(byte[]))
                        {
                            return (!string.IsNullOrEmpty(parameter)) ? Convert.FromBase64String(parameter) : new byte[] { };
                        }
                        else if (parameterType == typeof(Uri))
                        {
                            return (!string.IsNullOrEmpty(parameter)) ? new Uri(parameter, UriKind.RelativeOrAbsolute) : null;
                        }
                        else if (parameterType == typeof(object))
                        {
                            return parameter;
                        }
                        else
                        {
                            TypeConverter stringConverter = GetStringConverter(parameterType);
                            if (stringConverter == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                                    SR.Format(
                                    SR.TypeNotSupportedByQueryStringConverter,
                                    parameterType.ToString(), GetType().Name)));
                            }

                            return stringConverter.ConvertFromInvariantString(parameter);
                        }
                    }
            }
        }

        public virtual string ConvertValueToString(object parameter, Type parameterType)
        {
            if (parameterType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameterType));
            }

            if (parameterType.IsValueType && parameter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameter));
            }

            switch (Type.GetTypeCode(parameterType))
            {
                case TypeCode.Byte:
                    return XmlConvert.ToString((byte)parameter);
                case TypeCode.SByte:
                    return XmlConvert.ToString((sbyte)parameter);
                case TypeCode.Int16:
                    return XmlConvert.ToString((short)parameter);
                case TypeCode.Int32:
                    {
                        if (typeof(Enum).IsAssignableFrom(parameterType))
                        {
                            return Enum.Format(parameterType, parameter, "G");
                        }
                        else
                        {
                            return XmlConvert.ToString((int)parameter);
                        }
                    }
                case TypeCode.Int64:
                    return XmlConvert.ToString((long)parameter);
                case TypeCode.UInt16:
                    return XmlConvert.ToString((ushort)parameter);
                case TypeCode.UInt32:
                    return XmlConvert.ToString((uint)parameter);
                case TypeCode.UInt64:
                    return XmlConvert.ToString((ulong)parameter);
                case TypeCode.Single:
                    return XmlConvert.ToString((float)parameter);
                case TypeCode.Double:
                    return XmlConvert.ToString((double)parameter);
                case TypeCode.Char:
                    return XmlConvert.ToString((char)parameter);
                case TypeCode.Decimal:
                    return XmlConvert.ToString((decimal)parameter);
                case TypeCode.Boolean:
                    return XmlConvert.ToString((bool)parameter);
                case TypeCode.String:
                    return (string)parameter;
                case TypeCode.DateTime:
                    return XmlConvert.ToString((DateTime)parameter, XmlDateTimeSerializationMode.RoundtripKind);
                default:
                    {
                        if (parameterType == typeof(TimeSpan))
                        {
                            return XmlConvert.ToString((TimeSpan)parameter);
                        }
                        else if (parameterType == typeof(Guid))
                        {
                            return XmlConvert.ToString((Guid)parameter);
                        }
                        else if (parameterType == typeof(DateTimeOffset))
                        {
                            return XmlConvert.ToString((DateTimeOffset)parameter);
                        }
                        else if (parameterType == typeof(byte[]))
                        {
                            return (parameter != null) ? Convert.ToBase64String((byte[])parameter, Base64FormattingOptions.None) : null;
                        }
                        else if (parameterType == typeof(Uri) || parameterType == typeof(object))
                        {
                            // URI or object
                            return (parameter != null) ? Convert.ToString(parameter, CultureInfo.InvariantCulture) : null;
                        }
                        else
                        {
                            TypeConverter stringConverter = GetStringConverter(parameterType);
                            if (stringConverter == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(
                                    SR.Format(
                                    SR.TypeNotSupportedByQueryStringConverter,
                                    parameterType.ToString(), GetType().Name)));
                            }
                            else
                            {
                                return stringConverter.ConvertToInvariantString(parameter);
                            }
                        }
                    }
            }
        }

        // hash table is safe for multiple readers single writer
        private TypeConverter GetStringConverter(Type parameterType)
        {
            if (_typeConverterCache.ContainsKey(parameterType))
            {
                return (TypeConverter)_typeConverterCache[parameterType];
            }

            if (parameterType.GetCustomAttributes(typeof(TypeConverterAttribute), true) is TypeConverterAttribute[] typeConverterAttrs)
            {
                foreach (TypeConverterAttribute converterAttr in typeConverterAttrs)
                {
                    Type converterType = Type.GetType(converterAttr.ConverterTypeName, false, true);
                    if (converterType != null)
                    {
                        TypeConverter converter = null;
                        Exception handledException = null;
                        try
                        {
                            converter = (TypeConverter)Activator.CreateInstance(converterType);
                        }
                        catch (TargetInvocationException e)
                        {
                            handledException = e;
                        }
                        catch (MemberAccessException e)
                        {
                            handledException = e;
                        }
                        catch (TypeLoadException e)
                        {
                            handledException = e;
                        }
                        catch (COMException e)
                        {
                            handledException = e;
                        }
                        catch (InvalidComObjectException e)
                        {
                            handledException = e;
                        }
                        finally
                        {
                            if (handledException != null)
                            {
                                if (Fx.IsFatal(handledException))
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(handledException);
                                }
                                DiagnosticUtility.TraceHandledException(handledException, TraceEventType.Warning);
                            }
                        }

                        if (converter == null)
                        {
                            continue;
                        }

                        if (converter.CanConvertTo(typeof(string)) && converter.CanConvertFrom(typeof(string)))
                        {
                            _typeConverterCache.Add(parameterType, converter);
                            return converter;
                        }
                    }
                }
            }

            return null;
        }
    }
}
