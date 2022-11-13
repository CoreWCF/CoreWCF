// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.OpenApi.Attributes;
using CoreWCF.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace CoreWCF.OpenApi
{
    /// <summary>
    /// This class builds an OpenAPI specification file out of attributes applied to WCF service interfaces.
    /// </summary>
    public static class OpenApiSchemaBuilder
    {
        private const string ArrayNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
        private const string DataContractNamespace = "http://schemas.datacontract.org/2004/07/";

        /// <summary>
        /// Build the OpenAPI specification file.
        /// </summary>
        /// <param name="info">Top level information about the API.</param>
        /// <param name="contracts">One or more service contracts.</param>
        /// <returns>An OpenAPI specification file.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static OpenApiDocument BuildOpenApiSpecificationDocument(OpenApiOptions info, IEnumerable<OpenApiContractInfo> contracts)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            if (contracts == null)
            {
                throw new ArgumentNullException(nameof(contracts));
            }

            OpenApiDocument document = new OpenApiDocument
            {
                Components = new OpenApiComponents(),
                Paths = new OpenApiPaths()
            };

            PopulateOpenApiInfo(document, info);
            PopulateOpenApiPathsOperations(document, contracts, info.TagsToHide);

            if (info.TagsSorter != null)
            {
                var tags = document.Tags as List<OpenApiTag> ?? document.Tags.ToList();
                tags.Sort(info.TagsSorter);
                document.Tags = tags;
            }

            return document;
        }

        /// <summary>
        /// Populate some top level general info about the API.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="info">Top level information about the API.</param>
        private static void PopulateOpenApiInfo(OpenApiDocument document, OpenApiOptions info)
        {
            document.Info = new OpenApiInfo
            {
                Version = info.Version ?? "",
                Description = info.Description,
                Title = info.Title ?? "",
                TermsOfService = info.TermsOfService
            };

            if (info.ContactName != null || info.ContactEmail != null || info.ContactUrl != null)
            {
                document.Info.Contact = new OpenApiContact
                {
                    Name = info.ContactName,
                    Email = info.ContactEmail,
                    Url = info.ContactUrl
                };
            }

            if (info.LicenseName != null)
            {
                document.Info.License = new OpenApiLicense
                {
                    Name = info.LicenseName,
                    Url = info.LiceneUrl
                };
            }

            if (info.ExternalDocumentUrl != null)
            {
                document.ExternalDocs = new OpenApiExternalDocs
                {
                    Description = info.ExternalDocumentDescription,
                    Url = info.ExternalDocumentUrl
                };
            }
        }

        /// <summary>
        /// Populate the paths and operations from a given API.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="contracts">The WCF contracts that should be documented.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        private static void PopulateOpenApiPathsOperations(OpenApiDocument document, IEnumerable<OpenApiContractInfo> contracts, IEnumerable<string> tagsToHide)
        {
            foreach (OpenApiContractInfo contractInfo in contracts)
            {
                List<MethodInfo> methods = new List<MethodInfo>();
                foreach (Type interfaceInfo in contractInfo.Contract.GetInterfaces())
                {
                    methods.AddRange(interfaceInfo.GetMethods());
                }
                methods.AddRange(contractInfo.Contract.GetMethods());

                OpenApiBasePathAttribute basePathAttribute = contractInfo.Contract.GetCustomAttribute<OpenApiBasePathAttribute>();

                foreach (MethodInfo method in methods)
                {
                    PopulateOpenApiPath(
                        document,
                        method,
                        tagsToHide,
                        basePathAttribute?.BasePath,
                        contractInfo.ResponseFormat,
                        GetMethodUriWebGet);

                    PopulateOpenApiPath(
                        document,
                        method,
                        tagsToHide,
                        basePathAttribute?.BasePath,
                        contractInfo.ResponseFormat,
                        GetMethodUriWebInvoke);
                }
            }
        }

        /// <summary>
        /// Populate a path that uses a from a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="methodInfo">The given method.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        /// <param name="additionalBasePath">An additional base path a given service contract is registered under.</param>
        /// <param name="behaviorFormat">The default format in the WebHttpBehavior.</param>
        /// <param name="getOperationInfo">Get necessary information about an operation.</param>
        private static void PopulateOpenApiPath(
            OpenApiDocument document,
            MethodInfo methodInfo,
            IEnumerable<string> tagsToHide,
            string additionalBasePath,
            WebMessageFormat behaviorFormat,
            Func<MethodInfo, OperationInfo> getOperationInfo)
        {
            OperationInfo operationInfo = getOperationInfo(methodInfo);
            if (operationInfo.Method == null || operationInfo.UriTemplate == null)
            {
                return;
            }

            if (methodInfo.GetCustomAttribute<OpenApiHiddenAttribute>() != null)
            {
                return;
            }

            foreach (OpenApiTagAttribute tagAttribute in methodInfo.GetCustomAttributes<OpenApiTagAttribute>())
            {
                if (tagsToHide.Contains(tagAttribute.Tag))
                {
                    return;
                }
            }

            OpenApiOperation operation = new OpenApiOperation();

            string uri = Regex.Replace(operationInfo.UriTemplate, @"\?.*", "");

            if (!string.IsNullOrEmpty(additionalBasePath))
            {
                uri = additionalBasePath + uri;
            }

            DefaultContentType defaultContentType = new DefaultContentType
            {
                ResponseFormatExplicitlySet = operationInfo.IsResponseFormatSetExplicitly,
                ResponseAttributeFormat = operationInfo.ResponseFormat,
                ResponseBehaviorFormat = behaviorFormat
            };

            NameTable table = new NameTable();
            XmlNamespaceManager nsManager = new XmlNamespaceManager(table);

            PopulateOpenApiResponses(document, operation, methodInfo, defaultContentType, tagsToHide, nsManager);
            PopulateOpenApiParameters(document, operation, methodInfo, operationInfo.UriTemplate, defaultContentType, tagsToHide, nsManager);
            PopulateOpenApiOperationTags(document, operation, methodInfo);
            PopulateOpenApiOperationSummary(operation, methodInfo);

            OperationType? operationType = GetOperationType(operationInfo.Method);
            if (operationType.HasValue && document.Paths.ContainsKey(uri))
            {
                if (!document.Paths[uri].Operations.ContainsKey(operationType.Value))
                {
                    document.Paths[uri].Operations.Add(operationType.Value, operation);
                }
            }
            else if (operationType.HasValue)
            {
                document.Paths.Add(uri, new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        { operationType.Value, operation }
                    }
                });
            }
        }

        /// <summary>
        /// Maps an HTTP method to an OperationType.
        /// </summary>
        /// <param name="method">An HTTP method.</param>
        /// <returns>An OperationType.</returns>
        private static OperationType? GetOperationType(string method)
        {
            switch (method.ToLower())
            {
                case "get":
                    return OperationType.Get;
                case "put":
                    return OperationType.Put;
                case "post":
                    return OperationType.Post;
                case "delete":
                    return OperationType.Delete;
                case "options":
                    return OperationType.Options;
                case "head":
                    return OperationType.Head;
                case "patch":
                    return OperationType.Patch;
                case "trace":
                    return OperationType.Trace;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get the method and URI for a service contract method with a WebGetAttribute.
        /// </summary>
        /// <param name="methodInfo">A method in a service contract.</param>
        /// <returns>An HTTP method and URI.</returns>
        private static OperationInfo GetMethodUriWebGet(MethodInfo methodInfo)
        {
            WebGetAttribute attribute = methodInfo.GetCustomAttribute<WebGetAttribute>()
                ?? WebHttpServiceModelCompat.GetNativeAttribute<WebGetAttribute>(methodInfo);

            if (attribute == null)
            {
                return new OperationInfo();
            }

            return new OperationInfo
            {
                Method = "get",
                UriTemplate = attribute.UriTemplate,
                IsResponseFormatSetExplicitly = attribute.IsResponseFormatSetExplicitly,
                ResponseFormat = attribute.ResponseFormat,
                IsRequestFormatSetExplicitly = attribute.IsRequestFormatSetExplicitly,
                RequestFormat = attribute.RequestFormat
            };
        }

        /// <summary>
        /// Get the method and URI for a service contract method with a WebInvokeAttribute.
        /// </summary>
        /// <param name="methodInfo">A method in a service contract.</param>
        /// <returns>An HTTP method and URI.</returns>
        private static OperationInfo GetMethodUriWebInvoke(MethodInfo methodInfo)
        {
            WebInvokeAttribute attribute = methodInfo.GetCustomAttribute<WebInvokeAttribute>()
                ?? WebHttpServiceModelCompat.GetNativeAttribute<WebInvokeAttribute>(methodInfo);

            if (attribute == null)
            {
                return new OperationInfo();
            }

            return new OperationInfo
            {
                Method = attribute.Method?.ToLower(CultureInfo.InvariantCulture),
                UriTemplate = attribute.UriTemplate,
                IsResponseFormatSetExplicitly = attribute.IsResponseFormatSetExplicitly,
                ResponseFormat = attribute.ResponseFormat,
                IsRequestFormatSetExplicitly = attribute.IsRequestFormatSetExplicitly,
                RequestFormat = attribute.RequestFormat
            };
        }

        /// <summary>
        /// Populate the responses for a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="method">The given method.</param>
        /// <param name="defaultContentType">Calculates the default content type.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        private static void PopulateOpenApiResponses(
            OpenApiDocument document,
            OpenApiOperation operation,
            MethodInfo method,
            DefaultContentType defaultContentType,
            IEnumerable<string> tagsToHide,
            XmlNamespaceManager nsManager)
        {
            IEnumerable<OpenApiResponseAttribute> attributes = method.GetCustomAttributes<OpenApiResponseAttribute>();
            if (attributes.Any())
            {
                foreach (OpenApiResponseAttribute responseAttribute in method.GetCustomAttributes<OpenApiResponseAttribute>())
                {
                    PopulateOpenApiResponse(
                        responseAttribute.Type,
                        document,
                        operation,
                        defaultContentType,
                        tagsToHide,
                        nsManager,
                        responseAttribute);
                }
            }
            else if (method.ReturnType != null && method.ReturnType != typeof(Task))
            {
                Type type = method.ReturnType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    type = type.GetGenericArguments()[0];
                }

                PopulateOpenApiResponse(
                    type,
                    document,
                    operation,
                    defaultContentType,
                    tagsToHide,
                    nsManager);
            }
        }

        /// <summary>
        /// Populate a response for a given method.
        /// </summary>
        /// <param name="type">The type of the response.</param>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="defaultContentType">Calculates the default content type.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        /// <param name="nsManager">XmlNamespaceManager instance.</param>
        /// <param name="responseAttribute">Open API metadata for a response.</param>
        private static void PopulateOpenApiResponse(
            Type type,
            OpenApiDocument document,
            OpenApiOperation operation,
            DefaultContentType defaultContentType,
            IEnumerable<string> tagsToHide,
            XmlNamespaceManager nsManager,
            OpenApiResponseAttribute responseAttribute = null)
        {
            DataContractAttribute dataContractAttribute = type?.GetCustomAttribute<DataContractAttribute>();
            OpenApiSchema schemaSchema;

            if (type == null)
            {
                schemaSchema = null;
            }
            else if (!string.IsNullOrEmpty(dataContractAttribute?.Name))
            {
                PopulateOpenApiSchema(document, type, tagsToHide, nsManager);

                schemaSchema = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = dataContractAttribute.Name
                    }
                };
            }
            else
            {
                schemaSchema = new OpenApiSchema
                {
                    Type = GetType(type)
                };
            }

            OpenApiResponse response = new OpenApiResponse
            {
                Description = responseAttribute?.Description
            };

            if (responseAttribute?.ContentTypes != null)
            {
                response.Content = responseAttribute.ContentTypes.ToDictionary(contentType => contentType, _ => new OpenApiMediaType { Schema = schemaSchema });
            }
            else if (schemaSchema?.Reference != null)
            {
                response.Content = defaultContentType.GetContentTypes(true).ToDictionary(contentType => contentType, _ => new OpenApiMediaType { Schema = schemaSchema });
            }

            int statusCode = responseAttribute?.StatusCode == null ? 200 : (int)responseAttribute.StatusCode;

            operation.Responses.Add(statusCode.ToString(CultureInfo.InvariantCulture), response);
        }

        /// <summary>
        /// Populate the parameters for a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="method">The given method.</param>
        /// <param name="uriTemplateRaw">The uri template for the method.</param>
        /// <param name="defaultContentType">Calculates the default content type.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        /// <param name="nsManager">XmlNamespaceManager instance.</param>
        private static void PopulateOpenApiParameters(
            OpenApiDocument document,
            OpenApiOperation operation,
            MethodInfo method,
            string uriTemplateRaw,
            DefaultContentType defaultContentType,
            IEnumerable<string> tagsToHide,
            XmlNamespaceManager nsManager)
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (operation.Parameters == null)
                {
                    operation.Parameters = new List<OpenApiParameter>();
                }

                OpenApiParameterAttribute attribute = parameter.GetCustomAttribute<OpenApiParameterAttribute>();

                bool isHidden = false;
                foreach (OpenApiTagAttribute tagAttribute in parameter.GetCustomAttributes<OpenApiTagAttribute>())
                {
                    if (tagsToHide.Contains(tagAttribute.Tag))
                    {
                        isHidden = true;
                    }
                }

                OpenApiHiddenAttribute hiddenAttribute = parameter.GetCustomAttribute<OpenApiHiddenAttribute>();

                if (isHidden || hiddenAttribute != null)
                {
                    continue;
                }

                UriTemplate uriTemplate = new UriTemplate(uriTemplateRaw);
                ParameterLocation? parameterLocation = null;
                if (uriTemplate.PathSegmentVariableNames.Any(variableName => string.Equals(variableName, parameter.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    parameterLocation = ParameterLocation.Path;
                }
                else if (uriTemplate.QueryValueVariableNames.Any(variableName => string.Equals(variableName, parameter.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    parameterLocation = ParameterLocation.Query;
                }

                Dictionary<string, StringValues> queryString = QueryHelpers.ParseQuery(new Uri("http://microsoft.com" + uriTemplateRaw).Query);
                string name = parameter.Name;
                if (parameterLocation == ParameterLocation.Query && queryString.ContainsValue("{" + parameter.Name + "}"))
                {
                    KeyValuePair<string, StringValues> queryStringParameter = queryString.First(kvp => kvp.Value == "{" + parameter.Name + "}");
                    name = queryStringParameter.Key;
                }

                DataContractAttribute dataContractAttribute = parameter.ParameterType.GetCustomAttribute<DataContractAttribute>();
                if (parameterLocation == null)
                {
                    OpenApiMediaType content;

                    if (!string.IsNullOrEmpty(dataContractAttribute?.Name))
                    {
                        PopulateOpenApiSchema(document, parameter.ParameterType, tagsToHide, nsManager);

                        content = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.Schema,
                                    Id = dataContractAttribute.Name
                                }
                            }
                        };
                    }
                    else
                    {
                        content = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = GetType(parameter.ParameterType)
                            }
                        };
                    }

                    if (attribute?.ContentTypes != null)
                    {
                        operation.RequestBody = new OpenApiRequestBody
                        {
                            Content = attribute.ContentTypes.ToDictionary(contentType => contentType, _ => content),
                            Required = !parameter.IsOptional,
                            Description = attribute?.Description
                        };
                    }
                    else
                    {
                        operation.RequestBody = new OpenApiRequestBody
                        {
                            Content = defaultContentType.GetContentTypes(false).ToDictionary(contentType => contentType, _ => content),
                            Required = !parameter.IsOptional,
                            Description = attribute?.Description
                        };
                    }
                }
                else
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = name,
                        Description = attribute?.Description,
                        Schema = new OpenApiSchema
                        {
                            Type = GetType(parameter.ParameterType)
                        },
                        In = parameterLocation,
                        Required = !parameter.IsOptional || parameterLocation == ParameterLocation.Path
                    });
                }
            }
        }

        /// <summary>
        /// Populate the tags for a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The operation object that is being built up.</param>
        /// <param name="method">The given method.</param>
        private static void PopulateOpenApiOperationTags(OpenApiDocument document, OpenApiOperation operation, MethodInfo method)
        {
            foreach (OpenApiTagAttribute attribute in method.GetCustomAttributes<OpenApiTagAttribute>())
            {
                if (operation.Tags == null)
                {
                    operation.Tags = new List<OpenApiTag>();
                }

                operation.Tags.Add(new OpenApiTag { Name = attribute.Tag });

                if (!document.Tags.Any(existingTag => existingTag.Name == attribute.Tag))
                {
                    document.Tags.Add(new OpenApiTag
                    {
                        Name = attribute.Tag
                    });
                }
            }
        }

        /// <summary>
        /// Populate the operations summary for a given method.
        /// </summary>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="method">The given method.</param>
        private static void PopulateOpenApiOperationSummary(OpenApiOperation operation, MethodInfo method)
        {
            OpenApiOperationAttribute operationSummaryAttribute = method.GetCustomAttribute<OpenApiOperationAttribute>();
            if (operationSummaryAttribute != null)
            {
                operation.Summary = operationSummaryAttribute.Summary;
                operation.Description = operationSummaryAttribute.Description;
            }
        }

        /// <summary>
        /// Populate a schema from a given type.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="definition">The given type.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        /// <param name="nsManager">XmlNamespaceManager instance.</param>
        private static void PopulateOpenApiSchema(OpenApiDocument document, Type definition, IEnumerable<string> tagsToHide, XmlNamespaceManager nsManager)
        {
            bool IsContractAndIsNewContract(Type parentType, Type type, HashSet<string> seenKeys, bool isInArray)
            {
                DataContractAttribute parentDataContractAttribute = parentType?.GetCustomAttribute<DataContractAttribute>();
                DataContractAttribute dataContractAttribute = type?.GetCustomAttribute<DataContractAttribute>();

                if (dataContractAttribute?.Name == null)
                {
                    return false;
                }

                string schemaKey = GetSchemaKey(parentDataContractAttribute, dataContractAttribute, isInArray);
                if (seenKeys.Contains(schemaKey))
                {
                    return false;
                }
                else
                {
                    seenKeys.Add(schemaKey);
                }

                return !document.Components.Schemas.ContainsKey(GetSchemaKey(parentDataContractAttribute, dataContractAttribute, isInArray));
            }

            bool IsDataMemberProperty(PropertyInfo property) => property.GetCustomAttribute<DataMemberAttribute>() != null;

            HashSet<string> seenKeys = new HashSet<string>();

            if (!IsContractAndIsNewContract(null, definition, seenKeys, false))
            {
                return;
            }

            Queue<(Type parent, Type type, bool isInArray)> queue = new Queue<(Type parent, Type type, bool isInArary)>();
            queue.Enqueue((null, definition, false));

            while (queue.Any())
            {
                (Type parent, Type type, bool isInArray) = queue.Dequeue();
                AddTypeToSchemas(document, parent, type, isInArray, tagsToHide, nsManager);

                foreach (PropertyInfo property in type.GetProperties().Where(IsDataMemberProperty))
                {
                    if (IsContractAndIsNewContract(type, property.PropertyType, seenKeys, false))
                    {
                        queue.Enqueue((type, property.PropertyType, false));
                    }
                    else if (
                        property.PropertyType.GetInterface("IEnumerable") != null &&
                        property.PropertyType != typeof(string) &&
                        IsContractAndIsNewContract(type, property.PropertyType.GetGenericArguments().FirstOrDefault(), seenKeys, true))
                    {
                        queue.Enqueue((type, property.PropertyType.GetGenericArguments().FirstOrDefault(), true));
                    }
                }
            }
        }

        /// <summary>
        /// Get the type to store a schema under.
        /// </summary>
        /// <param name="parentDataContractAttribute">The data contract attribute for the parent of the type to add.</param>
        /// <param name="dataContractAttribute">The data contract attribute for the type to add.</param>
        /// <param name="dataContractAttribute">Whether the type is wrapped in an array or not.</param>
        /// <returns>The key to store the schema under.</returns>
        private static string GetSchemaKey(DataContractAttribute parentDataContractAttribute, DataContractAttribute dataContractAttribute, bool isInArray)
        {
            if (parentDataContractAttribute != null && isInArray)
            {
                return $"{parentDataContractAttribute.Name}-Array-{dataContractAttribute.Name}";
            }
            else if (parentDataContractAttribute != null)
            {
                return $"{parentDataContractAttribute.Name}-{dataContractAttribute.Name}";
            }
            else
            {
                return dataContractAttribute.Name;
            }
        }

        /// <summary>
        /// Add a specific definition to the schemas section.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="parent">The parent of the type to add.</param>
        /// <param name="type">The type to add.</param>
        /// <param name="type">Whether the type is in an array.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        /// <param name="nsManager">XmlNamespaceManager instance.</param>
        private static void AddTypeToSchemas(OpenApiDocument document, Type parent, Type type, bool isInArray, IEnumerable<string> tagsToHide, XmlNamespaceManager nsManager)
        {
            DataContractAttribute parentDataContractAttribute = parent?.GetCustomAttribute<DataContractAttribute>();
            DataContractAttribute dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();

            string parentNs = FindNamespace(parentDataContractAttribute?.Namespace ?? dataContractAttribute?.Namespace, parent ?? type);
            string parentPrefix = FindPrefix(nsManager, parentNs);

            string ns = FindNamespace(dataContractAttribute?.Namespace, type);
            string prefix = FindPrefix(nsManager, ns);

            Dictionary<string, IOpenApiExtension> xmlBlock = isInArray ?
                new Dictionary<string, IOpenApiExtension>
                {
                    {"xml", new OpenApiObject
                        {
                            {"name", new OpenApiString(dataContractAttribute?.Name ?? type.Name)},
                            {"namespace", new OpenApiString(ns)},
                            {"prefix", new OpenApiString(prefix)}
                        }
                    }
                } :
                new Dictionary<string, IOpenApiExtension>
                {
                    {"xml", new OpenApiObject
                        {
                            {"namespace", new OpenApiString(parentNs)},
                            {"prefix", new OpenApiString(parentPrefix)}
                        }
                    }
                };

            SortedSet<string> required = new SortedSet<string>();
            OpenApiSchema definitionSchema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>(),
                // The URI type might mangle the namespace so we do this manually.
                Extensions = xmlBlock,
            };

            IEnumerable<(PropertyInfo, DataMemberAttribute)> properties = type
                .GetProperties()
                .Select(property => (Property: property, DataMemberAttribute: property.GetCustomAttribute<DataMemberAttribute>()))
                .Where(property => property.DataMemberAttribute != null)
                .OrderBy(property => property.DataMemberAttribute.Order);

            foreach ((PropertyInfo property, DataMemberAttribute dataMemberAttribute) in properties)
            {
                OpenApiHiddenAttribute hiddenAttribute = property.GetCustomAttribute<OpenApiHiddenAttribute>();
                if (hiddenAttribute != null)
                {
                    continue;
                }

                bool isHidden = false;
                foreach (OpenApiTagAttribute tagAttribute in property.GetCustomAttributes<OpenApiTagAttribute>())
                {
                    if (tagsToHide.Contains(tagAttribute.Tag))
                    {
                        isHidden = true;
                    }
                }

                if (isHidden)
                {
                    continue;
                }

                string name = dataMemberAttribute.Name ?? property.Name;

                OpenApiPropertyAttribute memberPropertiesAttribute = property.GetCustomAttribute<OpenApiPropertyAttribute>();

                IEnumerable<CustomAttributeNamedArgument> memberPropertiesAttributeData = property
                        .GetCustomAttributesData()
                        .FirstOrDefault(data => data.AttributeType == typeof(OpenApiPropertyAttribute))
                        ?.NamedArguments;
                bool maxLengthSet = memberPropertiesAttributeData?.Any(arg => arg.MemberName == "MaxLength") ?? false;
                bool minLengthSet = memberPropertiesAttributeData?.Any(arg => arg.MemberName == "MinLength") ?? false;

                if (memberPropertiesAttribute?.IsRequired ?? false)
                {
                    required.Add(name);
                }

                DataContractAttribute innerDataContractAttribute = property.PropertyType.GetCustomAttribute<DataContractAttribute>();
                if (innerDataContractAttribute?.Name != null)
                {
                    DataContractAttribute innerDataMemberAttribute = property.PropertyType.GetCustomAttribute<DataContractAttribute>();

                    definitionSchema.Properties.Add(name, new OpenApiSchema
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.Schema,
                            Id = GetSchemaKey(dataContractAttribute, innerDataMemberAttribute, false)
                        },
                        Description = memberPropertiesAttribute?.Description
                    });
                }
                else if (property.PropertyType.GetInterface("IEnumerable") != null && property.PropertyType != typeof(string))
                {
                    // Handles the case of a custom collection that derives from a specialized generic collection.
                    Type innerType = property.PropertyType.GetGenericArguments().FirstOrDefault();
                    if (innerType == null && property.PropertyType.BaseType != null)
                    {
                        innerType = property.PropertyType.BaseType.GetGenericArguments().FirstOrDefault();
                    }

                    if (innerType != null)
                    {
                        DataContractAttribute innerDataMemberAttribute = innerType.GetCustomAttribute<DataContractAttribute>();
                        if (innerDataMemberAttribute != null)
                        {
                            definitionSchema.Properties.Add(name, new OpenApiSchema
                            {
                                Type = "array",
                                Description = memberPropertiesAttribute?.Description,
                                Items = new OpenApiSchema
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type = ReferenceType.Schema,
                                        Id = GetSchemaKey(dataContractAttribute, innerDataMemberAttribute, true),
                                    },
                                    Xml = new OpenApiXml
                                    {
                                        Namespace = new Uri(ArrayNamespace),
                                        Name = innerDataMemberAttribute.Name,
                                        Prefix = FindPrefix(nsManager, ArrayNamespace)
                                    }
                                },
                                // The URI type might mangle the namespace so we do this manually.
                                Extensions = new Dictionary<string, IOpenApiExtension>
                                {
                                    {"xml", new OpenApiObject
                                        {
                                            {"name", new OpenApiString(name) },
                                            {"namespace", new OpenApiString(parentNs)},
                                            {"prefix", new OpenApiString(parentPrefix)},
                                            {"wrapped", new OpenApiBoolean(true) }
                                        }
                                    }
                                },
                            });
                        }
                        else
                        {
                            string openApiType = GetType(innerType);

                            if (!string.IsNullOrEmpty(openApiType))
                            {
                                definitionSchema.Properties.Add(name, new OpenApiSchema
                                {
                                    Type = "array",
                                    Description = memberPropertiesAttribute?.Description,
                                    Items = new OpenApiSchema
                                    {
                                        Type = openApiType,
                                        Xml = new OpenApiXml
                                        {
                                            Namespace = new Uri(ArrayNamespace),
                                            Name = innerType.Name.ToLower(),
                                            Prefix = FindPrefix(nsManager, ArrayNamespace)
                                        }
                                    },
                                    // The URI type might mangle the namespace so we do this manually.
                                    Extensions = new Dictionary<string, IOpenApiExtension>
                                    {
                                        {"xml", new OpenApiObject
                                            {
                                                {"name", new OpenApiString(name) },
                                                {"namespace", new OpenApiString(parentNs)},
                                                {"prefix", new OpenApiString(parentPrefix)},
                                                {"wrapped", new OpenApiBoolean(true) }
                                            }
                                        }
                                    },
                                }); ;
                            }
                        }
                    }
                }
                else if (property.PropertyType.IsEnum)
                {
                    List<IOpenApiAny> enumValues = new List<IOpenApiAny>();
                    foreach (object value in Enum.GetValues(property.PropertyType))
                    {
                        enumValues.Add(new OpenApiString(value.ToString()));
                    }

                    definitionSchema.Properties.Add(name, new OpenApiSchema
                    {
                        Type = "string",
                        Description = memberPropertiesAttribute?.Description,
                        Enum = enumValues,
                        // The URI type might mangle the namespace so we do this manually.
                        Extensions = new Dictionary<string, IOpenApiExtension>
                        {
                            {"xml", new OpenApiObject
                                {
                                    {"namespace", new OpenApiString(ns)},
                                    {"prefix", new OpenApiString(prefix)}
                                }
                            }
                        }
                    });
                }
                else
                {
                    definitionSchema.Properties.Add(name, new OpenApiSchema
                    {
                        Type = GetType(property.PropertyType),
                        Description = memberPropertiesAttribute?.Description,
                        MinLength = minLengthSet ? memberPropertiesAttribute?.MinLength : null,
                        MaxLength = maxLengthSet ? memberPropertiesAttribute?.MaxLength : null,
                        Format = memberPropertiesAttribute?.Format,
                        // The URI type might mangle the namespace so we do this manually.
                        Extensions = new Dictionary<string, IOpenApiExtension>
                        {
                            {"xml", new OpenApiObject
                                {
                                    {"namespace", new OpenApiString(ns)},
                                    {"prefix", new OpenApiString(prefix)}
                                }
                            }
                        }
                    });
                }
            }

            definitionSchema.Required = required.Count > 0 ? required : null;

            document.Components.Schemas.Add(GetSchemaKey(parentDataContractAttribute, dataContractAttribute, isInArray), definitionSchema);
        }

        /// <summary>
        /// Figure out the valid namespace for XML serialization.
        /// </summary>
        /// <param name="ns">The namespace from the data contract.</param>
        /// <param name="type">The type itself.</param>
        /// <returns>A valid XML namespace if applicable.</returns>
        private static string FindNamespace(string ns, Type type)
        {
            if (ns == null)
            {
                return $"{DataContractNamespace}{type.Namespace}";
            }

            return ns;
        }

        /// <summary>
        /// Figure out a valid prefix for XML serialization.
        /// </summary>
        /// <param name="nsManager">XmlNamespaceManager instance.</param>
        /// <param name="ns">Valid namespace for XML serialization.</param>
        /// <returns>A valid XML prefix.</returns>
        private static string FindPrefix(XmlNamespaceManager nsManager, string ns)
        {
            string prefix = nsManager.LookupPrefix(ns);
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix;
            }

            int index = 0;
            IEnumerator enumerator = nsManager.GetEnumerator();
            while (enumerator.MoveNext())
            {
                index++;
            }
            index++;

            prefix = $"ns{index}";
            nsManager.AddNamespace(prefix, ns);
            return prefix;
        }

        /// <summary>
        /// Map a .NET type to JSON schema type.
        /// </summary>
        /// <param name="type">The type to be mapped.</param>
        /// <returns>The mapped type.</returns>
        private static string GetType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            Type actualType = IsNullable(type) ? Nullable.GetUnderlyingType(type) : type;

            if (actualType == typeof(int) || actualType == typeof(long) || actualType == typeof(short) || actualType == typeof(byte) || actualType == typeof(sbyte) || actualType == typeof(ushort) || actualType == typeof(ulong))
            {
                return "integer";
            }
            else if (actualType == typeof(float) || actualType == typeof(double) || actualType == typeof(decimal))
            {
                return "number";
            }
            else if (actualType == typeof(string) || actualType == typeof(DateTime) || actualType == typeof(Stream) || actualType == typeof(Guid) || actualType == typeof(DateTimeOffset) || actualType == typeof(char))
            {
                return "string";
            }
            else if (actualType == typeof(bool))
            {
                return "boolean";
            }

            return null;
        }

        /// <summary>
        /// Check if a type is nullable.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>Whether the type is nullable.</returns>
        private static bool IsNullable(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        /// <summary>
        /// Decides what the default content type should be.
        /// </summary>
        private class DefaultContentType
        {
            /// <summary>
            /// Whether the format was explicitly set in the WebGet/WebInvoke attribute for the response
            /// </summary>
            public bool ResponseFormatExplicitlySet { get; set; }

            /// <summary>
            /// The format set in the WebGet/WebInvoke attribute for the response.
            /// </summary>
            public WebMessageFormat ResponseAttributeFormat { get; set; }

            /// <summary>
            /// The default format in the WebHttpBehavior for the response.
            /// </summary>
            public WebMessageFormat ResponseBehaviorFormat { get; set; }

            /// <summary>
            /// Whether the format was explicitly set in the WebGet/WebInvoke attribute for the request
            /// </summary>
            public bool RequestFormatExplicitlySet { get; set; }

            /// <summary>
            /// The format set in the WebGet/WebInvoke attribute for the request.
            /// </summary>
            public WebMessageFormat RequestAttributeFormat { get; set; }

            /// <summary>
            /// The content type the default response should have.
            /// </summary>
            public IEnumerable<string> GetContentTypes(bool isResponse)
            {
                if (isResponse)
                {
                    WebMessageFormat format = ResponseFormatExplicitlySet ? ResponseAttributeFormat : ResponseBehaviorFormat;
                    switch (format)
                    {
                        case WebMessageFormat.Json:
                            return new string[] { "application/json" };
                        case WebMessageFormat.Xml:
                            return new string[] { "application/xml" };
                        default:
                            return null;
                    }
                }
                else
                {
                    if (RequestFormatExplicitlySet)
                    {
                        switch (RequestAttributeFormat)
                        {
                            case WebMessageFormat.Json:
                                return new string[] { "application/json", "text/json" };
                            case WebMessageFormat.Xml:
                                return new string[] { "application/xml", "text/xml" };
                            default:
                                return null;
                        }
                    }
                    else
                    {
                        return new string[] { "application/json", "text/json", "application/xml", "text/xml" };
                    }
                }
            }
        }

        /// <summary>
        /// Information about an operation.
        /// </summary>
        private class OperationInfo
        {
            /// <summary>
            /// Operation method.
            /// </summary>
            public string Method { get; set; }

            /// <summary>
            /// Operation URI.
            /// </summary>
            public string UriTemplate { get; set; }

            /// <summary>
            /// Whether the response format was explicitly set.
            /// </summary>
            public bool IsResponseFormatSetExplicitly { get; set; }

            /// <summary>
            /// The response format.
            /// </summary>
            public WebMessageFormat ResponseFormat { get; set; }

            /// <summary>
            /// Whether the request format was explicitly set.
            /// </summary>
            public bool IsRequestFormatSetExplicitly { get; set; }

            /// <summary>
            /// The request format.
            /// </summary>
            public WebMessageFormat RequestFormat { get; set; }
        }
    }
}
