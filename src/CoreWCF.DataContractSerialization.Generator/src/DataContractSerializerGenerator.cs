// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CoreWCF.DataContractSerialization.Generator;

/// <summary>
/// Generates reflection-free serializers for the data contracts listed on a
/// <c>DataContractSerializerContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generated serializers are an optimization, not a replacement: CoreWCF only uses them when the
/// <c>CoreWCF.Serialization.UseGeneratedDataContractSerializers</c> switch is on - which it is by
/// default only where dynamic code is unavailable, such as under Native AOT. Anything not generated
/// falls back to the reflection-based <c>DataContractSerializer</c>.
/// </para>
/// <para>
/// Enabled per target framework by CoreWCF.DataContractSerialization.targets, which turns it on for
/// .NET 8 and later. That gate is what lets the emitted code use a modern language version instead
/// of the oldest one any consumer might compile with.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed partial class DataContractSerializerGenerator : IIncrementalGenerator
{
    internal const string EnableProperty = "build_property.EnableCoreWCFDataContractSerializerGenerator";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<bool> enabled = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                options.GlobalOptions.TryGetValue(EnableProperty, out string? value)
                && string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase));

        // Parsing happens here, inside the transform, so its result is a plain value that Roslyn can
        // compare and cache. CoreWCF.BuildTools' generators instead collect syntax nodes and parse
        // inside RegisterSourceOutput, which means they re-run on every keystroke.
        IncrementalValuesProvider<ContextSpec> contexts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                Parser.SerializableAttributeName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (syntaxContext, cancellationToken) => Parser.Parse(syntaxContext, cancellationToken));

        context.RegisterSourceOutput(
            enabled.Combine(contexts.Collect()),
            static (productionContext, source) => Execute(source.Left, source.Right, productionContext));
    }

    private static void Execute(bool enabled, ImmutableArray<ContextSpec> contexts, SourceProductionContext context)
    {
        if (!enabled || contexts.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (ContextSpec contextSpec in contexts)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (DiagnosticInfo diagnostic in contextSpec.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (contextSpec.IsSuppressed)
            {
                continue;
            }

            Emitter emitter = new();
            SourceText source = emitter.Emit(contextSpec);

            // Reported after emitting, because whether a contract falls back is decided while the
            // source is being built rather than while it is being parsed.
            foreach (DiagnosticInfo diagnostic in emitter.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            context.AddSource(contextSpec.HintName, source);
        }
    }
}
