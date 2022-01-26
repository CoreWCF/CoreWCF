// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoreWCF.OpenApi.Attributes;
using CoreWCF.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;

namespace CoreWCF.OpenApi
{
    /// <summary>
    /// This class builds an OpenAPI specification file out of attributes applied to WCF service interfaces.
    /// </summary>
    public static class OpenApiSchemaBuilder
    {
        /// <summary>
        /// Build the OpenAPI specification file.
        /// </summary>
        /// <param name="info">Top level information about the API.</param>
        /// <param name="contracts">One or more service contracts.</param>
        /// <returns>An OpenAPI specification file.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static OpenApiDocument BuildOpenApiSpecificationDocument(OpenApiOptions info, IEnumerable<Type> contracts)
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
        private static void PopulateOpenApiPathsOperations(OpenApiDocument document, IEnumerable<Type> contracts, IEnumerable<string> tagsToHide)
        {
            foreach (Type contract in contracts)
            {
                var methods = new List<MethodInfo>();
                foreach (Type interfaceInfo in contract.GetInterfaces())
                {
                    methods.AddRange(interfaceInfo.GetMethods());
                }
                methods.AddRange(contract.GetMethods());

                OpenApiBasePathAttribute basePathAttribute = contract.GetCustomAttribute<OpenApiBasePathAttribute>();

                foreach (MethodInfo method in methods)
                {
                    PopulateOpenApiPath(document, method, tagsToHide, basePathAttribute?.BasePath, GetMethodUriWebGet);
                    PopulateOpenApiPath(document, method, tagsToHide, basePathAttribute?.BasePath, GetMethodUriWebInvoke);
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
        /// <param name="getMethodUri">Get the HTTP method and URI for a method.</param>
        private static void PopulateOpenApiPath(
            OpenApiDocument document,
            MethodInfo methodInfo,
            IEnumerable<string> tagsToHide,
            string additionalBasePath,
            Func<MethodInfo, (string method, string uriTemplate)> getMethodUri)
        {
            (string method, string uriTemplate) = getMethodUri(methodInfo);
            if (method == null || uriTemplate == null)
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

            string uri = Regex.Replace(uriTemplate, @"\?.*", "");

            if (!string.IsNullOrEmpty(additionalBasePath))
            {
                uri = additionalBasePath + uri;
            }
                
            PopulateOpenApiResponses(document, operation, methodInfo, tagsToHide);
            PopulateOpenApiParameters(document, operation, methodInfo, uriTemplate, tagsToHide);
            PopulateOpenApiOperationTags(operation, methodInfo);
            PopulateOpenApiOperationSummary(operation, methodInfo);


            OperationType? operationType = GetOperationType(method);
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
        private static (string method, string uriTemplate) GetMethodUriWebGet(MethodInfo methodInfo)
        {
            WebGetAttribute attribute = methodInfo.GetCustomAttribute<WebGetAttribute>();
            if (attribute == null)
            {
                return (null, null);
            }

            return ("get", attribute.UriTemplate);
        }

        /// <summary>
        /// Get the method and URI for a service contract method with a WebInvokeAttribute.
        /// </summary>
        /// <param name="methodInfo">A method in a service contract.</param>
        /// <returns>An HTTP method and URI.</returns>
        private static (string method, string uriTemplate) GetMethodUriWebInvoke(MethodInfo methodInfo)
        {
            WebInvokeAttribute attribute = methodInfo.GetCustomAttribute<WebInvokeAttribute>();
            if (attribute == null)
            {
                return (null, null);
            }

            return (attribute.Method.ToLower(CultureInfo.InvariantCulture), attribute.UriTemplate);
        }

        /// <summary>
        /// Populate the responses for a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="method">The given method.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        private static void PopulateOpenApiResponses(OpenApiDocument document, OpenApiOperation operation, MethodInfo method, IEnumerable<string> tagsToHide)
        {
            foreach (OpenApiResponseAttribute responseAttribute in method.GetCustomAttributes<OpenApiResponseAttribute>())
            {
                DataContractAttribute dataContractAttribute = responseAttribute.Type?.GetCustomAttribute<DataContractAttribute>();
                OpenApiSchema schemaSchema;
                if (responseAttribute.Type == null)
                {
                    schemaSchema = null;

                }
                else if (!string.IsNullOrEmpty(dataContractAttribute?.Name))
                {
                    PopulateOpenApiSchema(document, responseAttribute.Type, tagsToHide);

                    schemaSchema = new OpenApiSchema
                    {
                        
                        Reference =new OpenApiReference
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
                        Type = GetType(responseAttribute.Type)
                    };
                }

                OpenApiResponse response = new OpenApiResponse
                {
                    Description = responseAttribute.Description
                };

                if (responseAttribute.ContentTypes != null)
                {
                    response.Content = responseAttribute.ContentTypes.ToDictionary(contentType => contentType, _ => new OpenApiMediaType { Schema = schemaSchema });
                }

                operation.Responses.Add(((int)responseAttribute.StatusCode).ToString(CultureInfo.InvariantCulture), response);
            }
        }

        /// <summary>
        /// Populate the parameters for a given method.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="operation">The schema object that is being built up.</param>
        /// <param name="method">The given method.</param>
        /// <param name="uriTemplateRaw">The uri template for the method.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        private static void PopulateOpenApiParameters(OpenApiDocument document, OpenApiOperation operation, MethodInfo method, string uriTemplateRaw, IEnumerable<string> tagsToHide)
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (operation.Parameters == null)
                {
                    operation.Parameters = new List<OpenApiParameter>();
                }
                
                OpenApiParameterAttribute attribute = parameter.GetCustomAttribute<OpenApiParameterAttribute>();
                if (attribute == null)
                {
                    continue;
                }
                
                bool isHidden = false;
                foreach (OpenApiTagAttribute tagAttribute in parameter.GetCustomAttributes<OpenApiTagAttribute>())
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
                        PopulateOpenApiSchema(document, parameter.ParameterType, tagsToHide);

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

                    if (attribute.ContentTypes != null)
                    {
                        operation.RequestBody = new OpenApiRequestBody
                        {
                            Content = attribute.ContentTypes.ToDictionary(contentType => contentType, _ => content),
                            Required = attribute.IsRequired
                        };
                    }
                }
                else
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = name,
                        Description = attribute.Description,
                        Schema = new OpenApiSchema
                        {
                            Type = GetType(parameter.ParameterType)
                        },
                        In = parameterLocation,
                        Required = attribute.IsRequired || parameterLocation == ParameterLocation.Path
                    });
                }
            }
        }

        /// <summary>
        /// Populate the tags for a given method.
        /// </summary>
        /// <param name="operation">The operation object that is being built up.</param>
        /// <param name="method">The given method.</param>
        private static void PopulateOpenApiOperationTags(OpenApiOperation operation, MethodInfo method)
        {
            foreach (OpenApiTagAttribute attribute in method.GetCustomAttributes<OpenApiTagAttribute>())
            {
                if (operation.Tags == null)
                {
                    operation.Tags = new List<OpenApiTag>();
                }

                operation.Tags.Add(new OpenApiTag { Name = attribute.Tag });
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
        private static void PopulateOpenApiSchema(OpenApiDocument document, Type definition, IEnumerable<string> tagsToHide)
        {
            bool IsContractAndIsNewContract(Type type)
            {
                if (type == null)
                {
                    return false;
                }

                DataContractAttribute dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
                return dataContractAttribute?.Name != null && !document.Components.Schemas.ContainsKey(dataContractAttribute.Name);
            }

            bool IsDataMemberProperty(PropertyInfo property) => property.GetCustomAttribute<DataMemberAttribute>() != null;

            if (!IsContractAndIsNewContract(definition))
            {
                return;
            }

            Queue<Type> queue = new Queue<Type>();
            queue.Enqueue(definition);

            while (queue.Any())
            {
                Type currentType = queue.Dequeue();
                AddTypeToSchemas(document, currentType, tagsToHide);

                foreach (PropertyInfo property in currentType.GetProperties().Where(IsDataMemberProperty))
                {
                    if (IsContractAndIsNewContract(property.PropertyType))
                    {
                        queue.Enqueue(property.PropertyType);
                    }   
                    else if (property.PropertyType.GetInterface("IEnumerable") != null && property.PropertyType != typeof(string) && IsContractAndIsNewContract(property.PropertyType.GetGenericArguments().FirstOrDefault()))
                    {
                        queue.Enqueue(property.PropertyType.GetGenericArguments().FirstOrDefault());
                    }
                }
            }
        }

        /// <summary>
        /// Add a specific definition to the schemas section.
        /// </summary>
        /// <param name="document">The document object that is being built up.</param>
        /// <param name="type">The type to add.</param>
        /// <param name="tagsToHide">Any tags that need to be hidden for some reason.</param>
        private static void AddTypeToSchemas(OpenApiDocument document, Type type, IEnumerable<string> tagsToHide)
        {
            DataContractAttribute dataContractAttribute = type.GetCustomAttribute<DataContractAttribute>();
            if (document.Components.Schemas.ContainsKey(dataContractAttribute.Name))
            {
                return;
            }

            SortedSet<string> required = new SortedSet<string>();
            OpenApiSchema definitionSchema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>()
            };

            foreach (PropertyInfo property in type.GetProperties())
            {
                DataMemberAttribute dataMemberAttribute = property.GetCustomAttribute<DataMemberAttribute>();
                if (dataMemberAttribute == null)
                {
                    continue;
                }
                    
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
                            Id = innerDataMemberAttribute.Name
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
                                        Id = innerDataMemberAttribute.Name
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
                                    }
                                });
                            }
                        }
                    }
                }
                else
                {
                    definitionSchema.Properties.Add(name, new OpenApiSchema
                    {
                        Type = GetType(property.PropertyType),
                        Description = memberPropertiesAttribute?.Description,
                    });
                }
            }

            definitionSchema.Required = required.Count > 0 ? required : null;

            document.Components.Schemas.Add(dataContractAttribute.Name, definitionSchema);
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
    }
}
