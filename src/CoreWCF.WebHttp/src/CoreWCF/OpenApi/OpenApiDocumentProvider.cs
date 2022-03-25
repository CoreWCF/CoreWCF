// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Web;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace CoreWCF.OpenApi
{
    internal sealed class OpenApiDocumentProvider : ISwaggerProvider
    {
        private readonly IOptions<OpenApiOptions> _options;
        private OpenApiDocument _document;

        public OpenApiDocumentProvider(
            IOptions<OpenApiOptions> options)
        {
            _options = options;
        }

        public IList<OpenApiContractInfo> Contracts { get; } = new List<OpenApiContractInfo>();

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null)
        {
            if (_document == null)
            {
                _document = OpenApiSchemaBuilder.BuildOpenApiSpecificationDocument(_options.Value, Contracts);
            }

            return _document;
        }
    }
}
