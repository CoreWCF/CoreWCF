// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using CoreWCF.Collections.Generic;
using CoreWCF.Web;

namespace CoreWCF.Description;

/// <summary>
/// This class contains the logic to convert System.ServiceModel.Web attributes into
/// CoreWCF.WebHttp attributes.
/// This allows endpoints to be defined using WCF classic attributes as well as CoreWCF
/// attributes, for a simpler migration path.
/// </summary>
internal static class WebHttpServiceModelCompat
{
    private const string SmwWebGetAttributeFullName = "System.ServiceModel.Web.WebGetAttribute";
    private const string SmwWebInvokeAttributeFullName = "System.ServiceModel.Web.WebInvokeAttribute";

    private static readonly AssemblyName s_serviceModelWebName = new("System.ServiceModel.Web, PublicKeyToken=31bf3856ad364e35");

    private static readonly ReaderWriterLockSlim s_converterLock = new();
    private static Converter? s_converter;

    /// <summary>
    /// Registers a callback for when System.ServiceModel.Web is loaded, so we can
    /// emit the MSIL that does the attribute conversion.
    /// </summary>
    static WebHttpServiceModelCompat()
    {
        bool IsServiceModelWeb(Assembly assembly)
        {
            return AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), s_serviceModelWebName);
        }

        void SetConverter(Assembly smw)
        {
            s_converterLock.EnterWriteLock();
            try
            {
                if (s_converter == null
                    && smw.GetType(SmwWebGetAttributeFullName) is {} smwGetAttributeType
                    && smw.GetType(SmwWebInvokeAttributeFullName) is {} smwInvokeAttributeType)
                {
                    s_converter = new Converter(
                        ConverterBuilder<WebGetAttribute>.Build(smwGetAttributeType),
                        ConverterBuilder<WebInvokeAttribute>.Build(smwInvokeAttributeType));
                }
            }
            finally
            {
                s_converterLock.ExitWriteLock();
            }
        }

        var smw = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(IsServiceModelWeb);

        if (smw != null)
        {
            SetConverter(smw);
        }
        else
        {
            void CurrentDomainOnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
            {
                if (IsServiceModelWeb(args.LoadedAssembly))
                {
                    SetConverter(args.LoadedAssembly);
                    AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomainOnAssemblyLoad;
                }
            }
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainOnAssemblyLoad;
        }
    }

    public static void ServiceModelAttributeFixup(ServiceEndpoint endpoint)
    {
        if (TryGetConverter() is { } converter)
        {
            foreach (OperationDescription operationDescription in endpoint.Contract.Operations)
            {
                if (operationDescription.OperationBehaviors is KeyedByTypeCollection<IOperationBehavior> behaviors
                    && behaviors.Find<WebGetAttribute>() == null
                    && behaviors.Find<WebInvokeAttribute>() == null)
                {
                    converter.CheckForAndConvertSmAttributes(operationDescription);
                }
            }
        }
    }

    /// <summary>
    /// Double checked lock getter for the Converter.
    /// Ensures we only init Converter once, whilst
    /// limiting the cost of the check.
    /// </summary>
    /// <returns></returns>
    private static Converter? TryGetConverter()
    {
        if (s_converter != null)
            return s_converter;

        s_converterLock.EnterReadLock();
        try
        {
            return s_converter;
        }
        finally
        {
            s_converterLock.ExitReadLock();
        }
    }

    private class Converter
    {
        private readonly Func<IEnumerable<Attribute>, WebGetAttribute?> _convertGetAttribute;
        private readonly Func<IEnumerable<Attribute>, WebInvokeAttribute?> _convertInvokeAttribute;

        public Converter(Func<IEnumerable<Attribute>, WebGetAttribute?> convertGetAttribute, Func<IEnumerable<Attribute>, WebInvokeAttribute?> convertInvokeAttribute)
        {
            _convertGetAttribute = convertGetAttribute;
            _convertInvokeAttribute = convertInvokeAttribute;
        }

        public void CheckForAndConvertSmAttributes(OperationDescription od)
        {
            var opMethod = od.SyncMethod ?? od.TaskMethod ?? od.BeginMethod;
            var attributes = opMethod.GetCustomAttributes().ToArray();

            if (_convertInvokeAttribute(attributes) is { } convertedGetAttribute)
                od.OperationBehaviors.Add(convertedGetAttribute);


            if (_convertGetAttribute(attributes) is { } convertedInvokeAttribute)
                od.OperationBehaviors.Add(convertedInvokeAttribute);
        }
    }

    /// <summary>
    /// This static class builds the conversion functions using
    /// reflection and Linq Expressions
    /// </summary>
    private static class ConverterBuilder<TOut> where TOut : Attribute, new()
    {
        /// <summary>
        /// Creates a delegate that pattern matches for one type of attribute
        /// then converts it to the equivalent CoreWcf attribute.
        ///
        /// Since TInput comes from a reflected assembly, we only get Type,
        /// rather than a strong generic parameter. This means we need to
        /// use reflection on Type.
        /// </summary>
        /// <param name="inputType">The source attribute type</param>
        /// <returns>A CoreWCF attribute</returns>
        /// <typeparam name="TOut">The converted attribute type</typeparam>
        public static Func<IEnumerable<Attribute>, TOut?> Build(Type inputType)
        {
            return (Func<IEnumerable<Attribute>, TOut>) typeof(ConverterBuilder<TOut>)
                .GetMethod(nameof(BuildDynamic), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(inputType)
                .Invoke(null, Array.Empty<object>());
        }

        private static Func<IEnumerable<Attribute>, TOut?> BuildDynamic<TInput>()
        {
            var inputExpression = Expression.Parameter(typeof(TInput));

            // For each property build a get-set pair to copy properties
            // to the target value.
            var memberBindings =
                from inputProp in typeof(TInput).GetProperties()
                join outputProp in typeof(TOut).GetProperties() on inputProp.Name equals outputProp.Name
                where inputProp.CanRead
                where outputProp.CanWrite
                // Since we are dealing with Attributes, only primitive types are available
                // strings, numbers, and enums.
                // Therefore a simple cast expression should convert the enums simply.
                let value = Expression.Convert(Expression.Property(inputExpression, inputProp), outputProp.PropertyType)
                select (MemberBinding)Expression.Bind(outputProp, value);

            var convert = Expression.Lambda<Func<TInput, TOut>>(
                Expression.MemberInit(
                    Expression.New(typeof(TOut)),
                    memberBindings.ToArray()
                ),
                inputExpression
            ).Compile();

            return attributes =>
            {
                if (attributes.OfType<TInput>().SingleOrDefault() is { } input)
                {
                    // This is the same as
                    // return new CoreWCF.WebHttp.TOutAttribute()
                    // {
                    //     Foo = (CoreWCF.WebHttp.SomeEnum)input.Foo,
                    //     Bar = (CoreWCF.WebHttp.SomeEnum)input.Bar,
                    //     Baz = (CoreWCF.WebHttp.SomeEnum)input.Baz,
                    // };
                    return convert(input);
                }

                return null;
            };
        }
    }
}
