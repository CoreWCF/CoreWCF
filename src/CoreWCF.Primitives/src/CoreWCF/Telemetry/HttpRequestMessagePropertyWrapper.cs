// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace CoreWCF.Telemetry;


/// <summary>
/// This is a reflection-based wrapper around the HttpRequestMessageProperty class. It is done this way so we don't need to
/// have an explicit reference to System.ServiceModel.Http.dll. If the consuming application has a reference to
/// System.ServiceModel.Http.dll then the HttpRequestMessageProperty class will be available (IsHttpFunctionalityEnabled == true).
/// If the consuming application does not have a reference to System.ServiceModel.Http.dll then all http-related functionality
/// will be disabled (IsHttpFunctionalityEnabled == false).
/// </summary>
internal static class HttpRequestMessagePropertyWrapper
{
    private static readonly ReflectedInfo? ReflectedValues = Initialize();

    public static bool IsHttpFunctionalityEnabled => ReflectedValues != null;

    public static string Name
    {
        get
        {
            AssertHttpEnabled();
            return ReflectedValues!.Name;
        }
    }

    public static object CreateNew()
    {
        AssertHttpEnabled();
        return Activator.CreateInstance(ReflectedValues!.Type)!;
    }

    public static WebHeaderCollection GetHeaders(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        AssertIsFrameworkMessageProperty(httpRequestMessageProperty);
        return ReflectedValues!.HeadersFetcher.Fetch(httpRequestMessageProperty);
    }

    private static ReflectedInfo? Initialize()
    {
        Type? type = null;
        try
        {
            type = Type.GetType(
                "System.ServiceModel.Channels.HttpRequestMessageProperty, System.ServiceModel, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                true)!;

            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? throw new NotSupportedException("HttpRequestMessageProperty public parameterless constructor was not found");

            var headersProp = type.GetProperty("Headers", BindingFlags.Public | BindingFlags.Instance, null, typeof(WebHeaderCollection),
                                  Type.EmptyTypes, null)
                ?? throw new NotSupportedException("HttpRequestMessageProperty.Headers property not found");

            var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Static, null, typeof(string),
                               Type.EmptyTypes, null)
                ?? throw new NotSupportedException("HttpRequestMessageProperty.Name property not found");

            return nameProp.GetValue(null) is not string name
                ? throw new NotSupportedException("HttpRequestMessageProperty.Name property was null")
                : new ReflectedInfo(
                    type: type,
                    name: name,
                    headersFetcher: new PropertyFetcher<WebHeaderCollection>("Headers"));
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.HttpServiceModelReflectionFailedToBind(ex, type?.Assembly);
        }

        return null;
    }

    [Conditional("DEBUG")]
    private static void AssertHttpEnabled()
    {
        if (!IsHttpFunctionalityEnabled)
        {
            throw new InvalidOperationException("Http functionality is not enabled, check IsHttpFunctionalityEnabled before calling this method");
        }
    }

    [Conditional("DEBUG")]
    private static void AssertIsFrameworkMessageProperty(object httpRequestMessageProperty)
    {
        AssertHttpEnabled();
        if (httpRequestMessageProperty == null || !httpRequestMessageProperty.GetType().Equals(ReflectedValues!.Type))
        {
            throw new ArgumentException("Object must be of type HttpRequestMessageProperty");
        }
    }

    private sealed class ReflectedInfo
    {
        public readonly Type Type;
        public readonly string Name;
        public readonly PropertyFetcher<WebHeaderCollection> HeadersFetcher;

        public ReflectedInfo(Type type, string name, PropertyFetcher<WebHeaderCollection> headersFetcher)
        {
            this.Type = type;
            this.Name = name;
            this.HeadersFetcher = headersFetcher;
        }
    }
}
