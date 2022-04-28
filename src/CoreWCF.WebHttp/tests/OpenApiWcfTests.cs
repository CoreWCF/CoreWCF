// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using CoreWCF.OpenApi;
using CoreWCF.OpenApi.Attributes;
using CoreWCF.Web;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Xunit;

namespace CoreWCF.WebHttp.Tests
{
    public class OpenApiWcfTests
    {
        [Fact]
        public void SetsOpenApiVersion()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type>());

            string version = json
                .GetProperty("openapi")
                .GetString();

            Assert.Equal("3.0.1", version);
        }

        // Info

        [Fact]
        public void SetsInfo()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                Title = "Test API",
                Description = "Test Description",
                TermsOfService = new Uri("http://termsof.service"),
                Version = "0.1"
            };

            JsonElement json = GetJson(options, new List<Type>());

            string title = json
                .GetProperty("info")
                .GetProperty("title")
                .GetString();

            string description = json
                .GetProperty("info")
                .GetProperty("description")
                .GetString();

            string termsOfService = json
                .GetProperty("info")
                .GetProperty("termsOfService")
                .GetString();

            string version = json
                .GetProperty("info")
                .GetProperty("version")
                .GetString();

            Assert.Equal(options.Title, title);
            Assert.Equal(options.Description, description);
            Assert.Equal("http://termsof.service", termsOfService);
            Assert.Equal(options.Version, version);
        }

        [Fact]
        public void SetsContact()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                ContactName = "contact",
                ContactEmail = "contact@testapi.com",
                ContactUrl = new Uri("http://contact.us")
            };

            JsonElement json = GetJson(options, new List<Type>());

            string contactName = json
                .GetProperty("info")
                .GetProperty("contact")
                .GetProperty("name")
                .GetString();

            string contactEmail = json
                .GetProperty("info")
                .GetProperty("contact")
                .GetProperty("email")
                .GetString();

            string contactUrl = json
               .GetProperty("info")
               .GetProperty("contact")
               .GetProperty("url")
               .GetString();

            Assert.Equal(options.ContactName, contactName);
            Assert.Equal(options.ContactEmail, contactEmail);
            Assert.Equal("http://contact.us", contactUrl);
        }

        [Fact]
        public void SetsLicense()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                LicenseName = "License",
                LiceneUrl = new Uri("http://license.txt")
            };

            JsonElement json = GetJson(options, new List<Type>());

            string licenseName = json
                .GetProperty("info")
                .GetProperty("license")
                .GetProperty("name")
                .GetString();

            string licenseUrl = json
                .GetProperty("info")
                .GetProperty("license")
                .GetProperty("url")
                .GetString();

            Assert.Equal(options.LicenseName, licenseName);
            Assert.Equal("http://license.txt", licenseUrl);
        }

        [Fact]
        public void SetsExternalDocument()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                ExternalDocumentDescription = "Description",
                ExternalDocumentUrl = new Uri("http://external.document")
            };

            JsonElement json = GetJson(options, new List<Type>());

            string externalDocumentDescription = json
                .GetProperty("externalDocs")
                .GetProperty("description")
                .GetString();

            string externalDocumentUrl = json
                .GetProperty("externalDocs")
                .GetProperty("url")
                .GetString();

            Assert.Equal(options.ExternalDocumentDescription, externalDocumentDescription);
            Assert.Equal("http://external.document", externalDocumentUrl);
        }

        // Operations

        private interface IOperationCanBeHiddenByAttribute
        {
            [OpenApiHidden]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void OperationCanBeHiddenByAttribute()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IOperationCanBeHiddenByAttribute) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("paths")
                .GetProperty("/path"));
        }

        private interface IOperationCanBeHiddenByTag
        {
            [OpenApiTag("one")]
            [OpenApiTag("two")]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void OperationCanBeHiddenByTag()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                TagsToHide = new List<string> { "two" }
            };

            JsonElement json = GetJson(options, new List<Type> { typeof(IOperationCanBeHiddenByTag) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("paths")
                .GetProperty("/path"));
        }

        private interface IGroupsOperationsByPath
        {
            [WebGet(UriTemplate = "/path")]
            public void Get();

            [WebInvoke(UriTemplate = "/path", Method = "POST")]
            public void Post();
        }

        [Fact]
        public void GroupsOperationsByPath()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IGroupsOperationsByPath) });

            json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get");

            json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("post");
        }

        private interface IOperationTagsSet
        {
            [OpenApiTag("one")]
            [OpenApiTag("two")]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void OperationTagsSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IOperationTagsSet) });

            List<string> tags = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("tags")
                .EnumerateArray()
                .Select(tag => tag.ToString())
                .ToList();

            Assert.Equal("one", tags[0]);
            Assert.Equal("two", tags[1]);
        }

        private interface IOperationSummaryDescriptionSet
        {
            [OpenApiOperation(Summary = "summary", Description = "description")]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void OperationSummaryDescriptionSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IOperationSummaryDescriptionSet) });

            string summary = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("summary")
                .GetString();

            string description = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("description")
                .GetString();

            Assert.Equal("summary", summary);
            Assert.Equal("description", description);
        }

        private interface IOperationsFromMultipleContracts1
        {
            [WebGet(UriTemplate = "/path1")]
            public void Operation();
        }

        private interface IOperationsFromMultipleContracts2
        {
            [WebGet(UriTemplate = "/path2")]
            public void Operation();
        }


        [Fact]
        public void OperationsFromMultipleContracts()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IOperationsFromMultipleContracts1), typeof(IOperationsFromMultipleContracts2) });

            json
                .GetProperty("paths")
                .GetProperty("/path1")
                .GetProperty("get");

            json
                .GetProperty("paths")
                .GetProperty("/path2")
                .GetProperty("get");
        }

        private interface IOperationChild1
        {
            [WebGet(UriTemplate = "/path1")]
            public void Operation();
        }

        private interface IOperationChild2
        {
            [WebGet(UriTemplate = "/path2")]
            public void Operation();
        }

        private interface IOperationParent : IOperationChild1, IOperationChild2 { }

        [Fact]
        public void OperationsFromChildContracts()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IOperationParent) });

            json
                .GetProperty("paths")
                .GetProperty("/path1")
                .GetProperty("get");

            json
                .GetProperty("paths")
                .GetProperty("/path2")
                .GetProperty("get");
        }

        // Responses

        private interface IDefaultResponse
        {
            [WebGet(UriTemplate = "/path")]
            public string Operation();
        }

        [Fact]
        public void DefaultResponseAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IDefaultResponse) });

            json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");
        }

        [DataContract(Name = "SimpleResponse")]
        private class SimpleResponse { }

        private interface IDefaultResponseContentTypeFallthrough
        {
            [WebGet(UriTemplate = "/attribute", ResponseFormat = WebMessageFormat.Json)]
            public SimpleResponse FromAttribute();

            [WebGet(UriTemplate = "/behavior")]
            public SimpleResponse FromBehavior();
        }

        [Fact]
        public void DefaultResponseContentTypeFallsThrough()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IDefaultResponseContentTypeFallthrough) });

            JsonElement response = json
                .GetProperty("paths")
                .GetProperty("/attribute")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");

            response
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref");

            JsonElement json2 = GetJson(new OpenApiOptions(), new List<OpenApiContractInfo>
            {
                new OpenApiContractInfo
                {
                    Contract = typeof(IDefaultResponseContentTypeFallthrough),
                    ResponseFormat = WebMessageFormat.Json
                } 
            });

            JsonElement response2 = json2
                .GetProperty("paths")
                .GetProperty("/behavior")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");

            response2
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref");

        }

        private interface ITaskResponses
        {
            [WebGet(UriTemplate = "/noresponse")]
            public Task NoResponse();

            [WebGet(UriTemplate = "/response")]
            public Task<SimpleResponse> Response();
        }

        [Fact]
        public void DefaultResponseHandlesTaskResponses()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ITaskResponses) });

            JsonElement response = json
                .GetProperty("paths")
                .GetProperty("/response")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");

            response
                .GetProperty("content")
                .GetProperty("application/xml")
                .GetProperty("schema")
                .GetProperty("$ref");

            List<JsonProperty> response2 = json
                .GetProperty("paths")
                .GetProperty("/noresponse")
                .GetProperty("get")
                .GetProperty("responses")
                .EnumerateObject()
                .ToList();

            Assert.Empty(response2);
        }

        private interface IDefaultStatusCodeIsOk
        {
            [OpenApiResponse]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void ResponseStatusCodeDefaultsOk()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IDefaultStatusCodeIsOk) });

            json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");
        }

        private interface IStatusCodeResponseSet
        {
            [OpenApiResponse(StatusCode = HttpStatusCode.Accepted, Description = "description")]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void StatusCodeResponseSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IStatusCodeResponseSet) });

            string description = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("202")
                .GetProperty("description")
                .GetString();

            Assert.Equal("description", description);
        }

        private interface IComplexResponseSet
        {
            [OpenApiResponse(
                StatusCode = HttpStatusCode.Created,
                Description = "description",
                Type = typeof(SimpleResponse),
                ContentTypes = new[] { "application/json", "text/xml" })]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void ComplexResponseSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IComplexResponseSet) });

            JsonElement response = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("201");

            string reference1 = response
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();

            string reference2 = response
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("SimpleResponse");

            Assert.Equal("#/components/schemas/SimpleResponse", reference1);
            Assert.Equal("#/components/schemas/SimpleResponse", reference2);
        }

        private interface ISimpleResponseSet
        {
            [OpenApiResponse(
                StatusCode = HttpStatusCode.OK,
                Description = "description",
                Type = typeof(string),
                ContentTypes = new[] { "text/plain" })]
            [WebGet(UriTemplate = "/path")]
            public void Operation();
        }

        [Fact]
        public void SimpleResponseSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ISimpleResponseSet) });

            JsonElement response = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200");

            string type = response
                .GetProperty("content")
                .GetProperty("text/plain")
                .GetProperty("schema")
                .GetProperty("type")
                .GetString();

            Assert.Equal("string", type);
        }

        // Parameters

        private interface IParameterCanBeHiddenByTag
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            public void Operation(
                [OpenApiTag("one")][OpenApiParameter(ContentTypes = new[] { "text/plain" })] string body);
        }

        [Fact]
        public void ParameterCanBeHiddenByTag()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                TagsToHide = new[] { "one" }
            };

            JsonElement json = GetJson(options, new List<Type> { typeof(IParameterCanBeHiddenByTag) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("post")
                .GetProperty("parameters"));
        }

        private interface IParameterCanBeHiddenByAttribute
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            public void Operation(
                [OpenApiHidden][OpenApiParameter(ContentTypes = new[] { "text/plain" })] string body);
        }

        [Fact]
        public void ParameterCanBeHiddenByAttribute()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                TagsToHide = new[] { "one" }
            };

            JsonElement json = GetJson(options, new List<Type> { typeof(IParameterCanBeHiddenByAttribute) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("post")
                .GetProperty("parameters"));
        }

        private interface IPathParametersSet
        {
            [WebGet(UriTemplate = "/path/{one}/{two}")]
            public void Operation(
                string one,
                string two);
        }

        [Fact]
        public void PathParametersSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IPathParametersSet) });

            List<JsonElement> parameters = json
                .GetProperty("paths")
                .GetProperty("/path/{one}/{two}")
                .GetProperty("get")
                .GetProperty("parameters")
                .EnumerateArray()
                .ToList();

            JsonElement param1 = parameters[0];
            Assert.Equal("one", param1.GetProperty("name").GetString());
            Assert.Equal("path", param1.GetProperty("in").GetString());
            Assert.True(param1.GetProperty("required").GetBoolean());
            Assert.Equal("string", param1.GetProperty("schema").GetProperty("type").GetString());

            JsonElement param2 = parameters[1];
            Assert.Equal("two", param2.GetProperty("name").GetString());
            Assert.Equal("path", param2.GetProperty("in").GetString());
            Assert.True(param2.GetProperty("required").GetBoolean());
            Assert.Equal("string", param2.GetProperty("schema").GetProperty("type").GetString());
        }

        private interface IQueryParametersSet
        {
            [WebGet(UriTemplate = "/path?one={one}&two={two}")]
            public void Operation(
                string one,
                string two);
        }

        [Fact]
        public void QueryParametersSet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IQueryParametersSet) });

            List<JsonElement> parameters = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("parameters")
                .EnumerateArray()
                .ToList();

            JsonElement param1 = parameters[0];
            Assert.Equal("one", param1.GetProperty("name").GetString());
            Assert.Equal("query", param1.GetProperty("in").GetString());
            Assert.True(param1.GetProperty("required").GetBoolean());
            Assert.Equal("string", param1.GetProperty("schema").GetProperty("type").GetString());

            JsonElement param2 = parameters[1];
            Assert.Equal("two", param2.GetProperty("name").GetString());
            Assert.Equal("query", param2.GetProperty("in").GetString());
            Assert.True(param2.GetProperty("required").GetBoolean());
            Assert.Equal("string", param2.GetProperty("schema").GetProperty("type").GetString());
        }

        private interface IParameterOptional
        {
            [WebGet(UriTemplate = "/path?one={one}")]
            public void Operation(
                string one = null);
        }

        [Fact]
        public void ParameterCanBeOptional()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IParameterOptional) });

            List<JsonElement> parameters = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("get")
                .GetProperty("parameters")
                .EnumerateArray()
                .ToList();

            JsonElement param1 = parameters[0];
            Assert.Throws<KeyNotFoundException>(() => param1.GetProperty("required"));
        }

        [DataContract(Name = "SimpleRequest")]
        internal class SimpleRequest { }

        private interface IRequestBodyContentTypeFallthrough
        {
            [WebInvoke(Method = "POST", UriTemplate = "/attribute", RequestFormat = WebMessageFormat.Json)]
            public void FromAttribute(SimpleRequest request);

            [WebInvoke(Method = "POST", UriTemplate = "/default")]
            public void Default(SimpleRequest request);
        }

        [Fact]
        public void DefaultRequestBodyContentTypeFallsThrough()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IRequestBodyContentTypeFallthrough) });

            JsonElement body = json
                .GetProperty("paths")
                .GetProperty("/attribute")
                .GetProperty("post")
                .GetProperty("requestBody");

            body
                .GetProperty("content")
                .GetProperty("application/json");

            body
                .GetProperty("content")
                .GetProperty("text/json");

            JsonElement body2 = json
                .GetProperty("paths")
                .GetProperty("/default")
                .GetProperty("post")
                .GetProperty("requestBody");

            body2
                .GetProperty("content")
                .GetProperty("application/json");

            body2
                .GetProperty("content")
                .GetProperty("text/json");
        }

        private interface ISimpleRequestBodySet
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            public void Operation(
                [OpenApiParameter(ContentTypes = new[] { "text/plain" })] string body);
        }

        [Fact]
        public void SimpleRequestBodySet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ISimpleRequestBodySet) });

            JsonElement body = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("post")
                .GetProperty("requestBody");

            string type = body
                .GetProperty("content")
                .GetProperty("text/plain")
                .GetProperty("schema")
                .GetProperty("type")
                .GetString();

            bool required = body
                .GetProperty("required")
                .GetBoolean();

            Assert.Equal("string", type);
            Assert.True(required);
        }

        private interface IComplexRequestBodySet
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            public void Operation(
                [OpenApiParameter(ContentTypes = new[] { "application/json" })] SimpleRequest body);
        }

        [Fact]
        public void ComplexRequestBodySet()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IComplexRequestBodySet) });

            JsonElement body = json
                .GetProperty("paths")
                .GetProperty("/path")
                .GetProperty("post")
                .GetProperty("requestBody");

            string reference = body
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("SimpleRequest");

            Assert.Equal("#/components/schemas/SimpleRequest", reference);
        }

        // Schema

        [DataContract(Name = "SimpleSchema")]
        private class SimpleSchema { }

        private interface ISchemaOnlyAddedOnce
        {
            [WebInvoke(Method = "POST", UriTemplate = "/one")]
            void One([OpenApiParameter(ContentTypes = new[] { "application/json" })] SimpleSchema body);

            [WebInvoke(Method = "POST", UriTemplate = "/two")]
            void Two([OpenApiParameter(ContentTypes = new[] { "application/json" })] SimpleSchema body);
        }

        [Fact]
        public void SchemaOnlyAddedOnce()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ISchemaOnlyAddedOnce) });

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("SimpleSchema");
        }

        [DataContract(Name = "CircularSchemaOne")]
        private class CircularSchemaOne
        {
            public CircularSchemaOne One { get; set; }

            public CircularSchemaTwo Two { get; set; }
        }

        [DataContract(Name = "CircularSchemaTwo")]
        private class CircularSchemaTwo
        {
            public CircularSchemaOne One { get; set; }

            public CircularSchemaTwo Two { get; set; }
        }

        private interface ICircularSchemaWorks
        {
            [WebInvoke(Method = "POST", UriTemplate = "/one")]
            void One([OpenApiParameter(ContentTypes = new[] { "application/json" })] CircularSchemaOne body);

            [WebInvoke(Method = "POST", UriTemplate = "/two")]
            void Two([OpenApiParameter(ContentTypes = new[] { "application/json" })] CircularSchemaTwo body);
        }

        [Fact]
        public void CircularSchemaWorks()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ICircularSchemaWorks) });

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CircularSchemaOne");

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CircularSchemaTwo");
        }

        [DataContract(Name = "NestedClassOne")]
        private class NestedClassOne
        {
            [DataMember(Name = "Two")]
            public NestedClassTwo Two { get; set; }
        }

        [DataContract(Name = "NestedClassTwo")]
        private class NestedClassTwo
        {
            [DataMember(Name = "Three")]
            public NestedClassThree Three { get; set; }
        }

        [DataContract(Name = "NestedClassThree")]
        private class NestedClassThree { }

        private interface INestedTypesAddedToSchemas
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] NestedClassOne body);
        }

        [Fact]
        public void NestedTypesAddedToSchemas()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(INestedTypesAddedToSchemas) });

            string referenceOne = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("NestedClassOne")
                .GetProperty("properties")
                .GetProperty("Two")
                .GetProperty("$ref")
                .GetString();

            string referenceTwo = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("NestedClassOne-NestedClassTwo")
                .GetProperty("properties")
                .GetProperty("Three")
                .GetProperty("$ref")
                .GetString();

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("NestedClassTwo-NestedClassThree");

            Assert.Equal("#/components/schemas/NestedClassOne-NestedClassTwo", referenceOne);
            Assert.Equal("#/components/schemas/NestedClassTwo-NestedClassThree", referenceTwo);
        }

        [DataContract(Name = "CollectionClass")]
        private class CollectionClass
        {
            [DataMember(Name = "Collection")]
            public List<CollectionInnerClass> Collection { get; set; }
        }

        [DataContract(Name = "CollectionInnerClass")]
        private class CollectionInnerClass { }

        private interface IGenericCollectionTypeAddedToSchemas
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] CollectionClass body);
        }

        [Fact]
        public void GenericCollectionTypeAddedToSchemas()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IGenericCollectionTypeAddedToSchemas) });

            string type = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CollectionClass")
                .GetProperty("properties")
                .GetProperty("Collection")
                .GetProperty("type")
                .GetString();

            string reference = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CollectionClass")
                .GetProperty("properties")
                .GetProperty("Collection")
                .GetProperty("items")
                .GetProperty("$ref")
                .GetString();

            json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CollectionClass-Array-CollectionInnerClass");

            Assert.Equal("array", type);
            Assert.Equal("#/components/schemas/CollectionClass-Array-CollectionInnerClass", reference);
        }

        [DataContract(Name = "NoDataMemberClass")]
        private class NoDataMemberClass
        {
            public string Property { get; set; }
        }

        private interface IPropertyMustBeDataMemberToBeAddedToSchema
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] NoDataMemberClass body);
        }

        [Fact]
        public void PropertyMustBeDataMemberToBeAddedToSchema()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IPropertyMustBeDataMemberToBeAddedToSchema) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("NoDataMemberClass")
                .GetProperty("properties"));
        }

        [DataContract(Name = "PropertyHiddenWithAttributeClass")]
        private class PropertyHiddenWithAttributeClass
        {
            [OpenApiHidden]
            [DataMember(Name = "Property")]
            public string Property { get; set; }
        }

        private interface IPropertyCanBeHiddenWithAttribute
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] PropertyHiddenWithAttributeClass body);
        }

        [Fact]
        public void PropertyCanBeHiddenWithAttribute()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IPropertyCanBeHiddenWithAttribute) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("PropertyHiddenWithAttributeClass")
                .GetProperty("properties"));
        }

        [DataContract(Name = "PropertyHiddenWithTagClass")]
        private class PropertyHiddenWithTagClass
        {
            [OpenApiTag("one")]
            [OpenApiTag("two")]
            [DataMember(Name = "Property")]
            public string Property { get; set; }
        }

        private interface IPropertyCanBeHiddenWithTag
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] PropertyHiddenWithTagClass body);
        }

        [Fact]
        public void PropertyCanBeHiddenWithTag()
        {
            OpenApiOptions options = new OpenApiOptions
            {
                TagsToHide = new[] { "two" }
            };

            JsonElement json = GetJson(options, new List<Type> { typeof(IPropertyCanBeHiddenWithTag) });

            Assert.Throws<KeyNotFoundException>(() => json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("PropertyHiddenWithTagClass")
                .GetProperty("properties"));
        }

        [DataContract(Name = "SimplePropertyClass")]
        private class SimplePropertyClass
        {
            [DataMember(Name = "Property")]
            [OpenApiProperty(Description = "description", IsRequired = true)]
            public string Property { get; set; }
        }

        private interface ISimplePropertyAdded
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] SimplePropertyClass body);
        }

        [Fact]
        public void SimplePropertyAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ISimplePropertyAdded) });

            JsonElement schema = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("SimplePropertyClass");

            string schemaType = schema
                .GetProperty("type")
                .GetString();

            List<JsonElement> required = schema
                .GetProperty("required")
                .EnumerateArray()
                .ToList();

            string type = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("type")
                .GetString();

            string description = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("description")
                .GetString();

            Assert.Equal("object", schemaType);
            JsonElement property = Assert.Single(required);
            Assert.Equal("Property", property.GetString());
            Assert.Equal("string", type);
            Assert.Equal("description", description);
        }

        [DataContract(Name = "ComplexPropertyClass")]
        private class ComplexPropertyClass
        {
            [DataMember(Name = "Property")]
            [OpenApiProperty(Description = "description", IsRequired = true)]
            public ComplexPropertyInnerClass Property { get; set; }
        }

        [DataContract(Name = "ComplexPropertyInnerClass")]
        private class ComplexPropertyInnerClass { }

        private interface IComplexPropertyAdded
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] ComplexPropertyClass body);
        }

        [Fact]
        public void ComplexPropertyAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IComplexPropertyAdded) });

            JsonElement schema = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("ComplexPropertyClass");

            string schemaType = schema
                .GetProperty("type")
                .GetString();

            List<JsonElement> required = schema
                .GetProperty("required")
                .EnumerateArray()
                .ToList();

            string reference = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("$ref")
                .GetString();

            Assert.Equal("object", schemaType);
            JsonElement property = Assert.Single(required);
            Assert.Equal("Property", property.GetString());
            Assert.Equal("#/components/schemas/ComplexPropertyClass-ComplexPropertyInnerClass", reference);
        }

        [DataContract(Name = "SimpleCollectionPropertyClass")]
        private class SimpleCollectionPropertyClass
        {
            [DataMember(Name = "Property")]
            [OpenApiProperty(Description = "description", IsRequired = true)]
            public List<string> Property { get; set; }
        }

        private interface ISimpleCollectionPropertyAdded
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] SimpleCollectionPropertyClass body);
        }

        [Fact]
        public void SimpleCollectionPropertyAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(ISimpleCollectionPropertyAdded) });

            JsonElement schema = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("SimpleCollectionPropertyClass");

            string schemaType = schema
                .GetProperty("type")
                .GetString();

            List<JsonElement> required = schema
                .GetProperty("required")
                .EnumerateArray()
                .ToList();

            string type = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("type")
                .GetString();

            string itemType = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("items")
                .GetProperty("type")
                .GetString();

            string description = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("description")
                .GetString();

            Assert.Equal("object", schemaType);
            JsonElement property = Assert.Single(required);
            Assert.Equal("Property", property.GetString());
            Assert.Equal("array", type);
            Assert.Equal("string", itemType);
            Assert.Equal("description", description);
        }

        [DataContract(Name = "ComplexCollectionPropertyClass")]
        private class ComplexCollectionPropertyClass
        {
            [DataMember(Name = "Property")]
            [OpenApiProperty(Description = "description", IsRequired = true)]
            public List<InnerComplexCollectionPropertyClass> Property { get; set; }
        }

        [DataContract(Name = "InnerComplexCollectionPropertyClass")]
        private class InnerComplexCollectionPropertyClass { }

        private interface IComplexCollectionPropertyAdded
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] ComplexCollectionPropertyClass body);
        }

        [Fact]
        public void ComplexCollectionPropertyAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IComplexCollectionPropertyAdded) });

            JsonElement schema = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("ComplexCollectionPropertyClass");

            string schemaType = schema
                .GetProperty("type")
                .GetString();

            List<JsonElement> required = schema
                .GetProperty("required")
                .EnumerateArray()
                .ToList();

            string type = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("type")
                .GetString();

            string itemType = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("items")
                .GetProperty("$ref")
                .GetString();

            string description = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("description")
                .GetString();

            Assert.Equal("object", schemaType);
            JsonElement property = Assert.Single(required);
            Assert.Equal("Property", property.GetString());
            Assert.Equal("array", type);
            Assert.Equal("#/components/schemas/ComplexCollectionPropertyClass-Array-InnerComplexCollectionPropertyClass", itemType);
            Assert.Equal("description", description);
        }

        private enum SimpleEnum
        {
            One,
            Two
        }

        [DataContract(Name = "EnumPropertyClass")]
        private class EnumPropertyClass
        {
            [DataMember(Name = "Property")]
            [OpenApiProperty(Description = "description", IsRequired = true)]
            public SimpleEnum Property { get; set; }
        }

        private interface IEnumPropertyAdded
        {
            [WebInvoke(Method = "POST", UriTemplate = "/path")]
            void Operation([OpenApiParameter(ContentTypes = new[] { "application/json" })] EnumPropertyClass body);
        }

        [Fact]
        public void EnumCollectionPropertyAdded()
        {
            JsonElement json = GetJson(new OpenApiOptions(), new List<Type> { typeof(IEnumPropertyAdded) });

            JsonElement schema = json
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("EnumPropertyClass");

            string schemaType = schema
                .GetProperty("type")
                .GetString();

            List<JsonElement> required = schema
                .GetProperty("required")
                .EnumerateArray()
                .ToList();

            string type = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("type")
                .GetString();

            List<string> values = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToList();

            string description = schema
                .GetProperty("properties")
                .GetProperty("Property")
                .GetProperty("description")
                .GetString();

            Assert.Equal("object", schemaType);
            JsonElement property = Assert.Single(required);
            Assert.Equal("Property", property.GetString());
            Assert.Equal("string", type);
            Assert.Equal("One", values[0]);
            Assert.Equal("Two", values[1]);
            Assert.Equal("description", description);
        }

        private static JsonElement GetJson(OpenApiOptions options, IEnumerable<OpenApiContractInfo> contracts)
        {
            OpenApiDocument document = OpenApiSchemaBuilder.BuildOpenApiSpecificationDocument(options, contracts);
            using (var textWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                OpenApiJsonWriter jsonWriter = new OpenApiJsonWriter(textWriter);
                document.SerializeAsV3(jsonWriter);

                string json = textWriter.ToString();
                return JsonDocument.Parse(json).RootElement;
            }
        }

        private static JsonElement GetJson(OpenApiOptions options, IEnumerable<Type> contracts) => GetJson(options, contracts.Select(contract => new OpenApiContractInfo { Contract = contract }));
    }
}
