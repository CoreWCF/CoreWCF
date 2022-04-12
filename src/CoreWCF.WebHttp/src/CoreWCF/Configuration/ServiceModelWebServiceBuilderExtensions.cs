// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Collections.Generic;
using CoreWCF.Description;
using CoreWCF.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace CoreWCF.Configuration
{
    public static class ServiceModelWebServiceBuilderExtensions
    {
        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            string address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                address,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            WebHttpBinding binding,
            string address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                binding,
                address,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            Uri address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                address,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            WebHttpBinding binding,
            Uri address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                binding,
                address,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            string address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            WebHttpBinding binding,
            string address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                binding,
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService, TContract>(
            this IServiceBuilder builder,
            WebHttpBinding binding,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                typeof(TContract),
                binding,
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            string address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                address,
                (Uri)null,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            WebHttpBinding binding,
            string address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                binding,
                address,
                (Uri)null,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            Uri address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                address,
                (Uri)null,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            WebHttpBinding binding,
            Uri address,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                binding,
                address,
                (Uri)null,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            string address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                new Uri(address, UriKind.RelativeOrAbsolute),
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            WebHttpBinding binding,
            string address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint<TService>(
                builder,
                implementedContract,
                binding,
                new Uri(address, UriKind.RelativeOrAbsolute),
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint(
                builder,
                typeof(TService),
                implementedContract,
                new WebHttpBinding(),
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint<TService>(
            this IServiceBuilder builder,
            Type implementedContract,
            WebHttpBinding binding,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint(
                builder,
                typeof(TService),
                implementedContract,
                binding,
                address,
                listenUri,
                configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint(
            this IServiceBuilder builder,
            Type service,
            Type implementedContract,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null) => AddServiceWebEndpoint(
        builder,
        service,
        implementedContract,
        new WebHttpBinding(),
        address,
        listenUri,
        configureWebBehavior);

        public static IServiceBuilder AddServiceWebEndpoint(
            this IServiceBuilder builder,
            Type service,
            Type implementedContract,
            WebHttpBinding binding,
            Uri address,
            Uri listenUri,
            Action<WebHttpBehavior> configureWebBehavior = null)
        {
            builder.AddServiceEndpoint(service, implementedContract, binding, address, listenUri, serviceEndpoint =>
            {
                KeyedByTypeCollection<IEndpointBehavior> behaviors = (KeyedByTypeCollection<IEndpointBehavior>)serviceEndpoint.EndpointBehaviors;
                WebHttpBehavior webHttpBehavior = behaviors.Find<WebHttpBehavior>() ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ServiceModelWebServicesNotRegistered, address)));
                configureWebBehavior?.Invoke(webHttpBehavior);

                if (webHttpBehavior.HelpEnabled)
                {
                    OpenApiDocumentProvider documentProvider = webHttpBehavior.ServiceProvider.GetRequiredService<OpenApiDocumentProvider>();
                    documentProvider.Contracts.Add(new OpenApiContractInfo
                    {
                        Contract = implementedContract,
                        ResponseFormat = webHttpBehavior.DefaultOutgoingResponseFormat
                    });
                }
            });

            return builder;
        }
    }
}
