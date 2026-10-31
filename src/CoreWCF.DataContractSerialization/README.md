# CoreWCF.DataContractSerialization

An AOT-safe, source-generated replacement for `DataContractSerializer` in CoreWCF.

`DataContractSerializer` discovers types by reflection and is annotated `[RequiresDynamicCode]` +
`[RequiresUnreferencedCode]`, so CoreWCF services cannot publish cleanly with `PublishAot` or
`PublishTrimmed`. This package will provide a Roslyn generator that reads `[DataContract]` and
`[DataMember]` at compile time and emits a reflection-free serializer per type, plugged into CoreWCF
through `DataContractSerializerOperationBehavior.CreateSerializer`.

**Status: milestone 1.** No generator code exists yet. What exists is the oracle it will be measured
against - a corpus of contract instances whose exact serialized bytes are recorded from the real
reflection-based serializer.

## Layout

| Project | Role |
| --- | --- |
| `src/CoreWCF.DataContractSerialization` | The shipped package (`netstandard2.0`). Empty so far; packs the generator as an analyzer. |
| `src/CoreWCF.DataContractSerialization.Generator` | The Roslyn generator. Empty so far. |
| `src/CoreWCF.DataContractSerialization.TestCorpus` | Contract types and the catalog of instances. Pure BCL - no CoreWCF reference, no reflection - so it stays publishable ahead-of-time. |
| `src/CoreWCF.DataContractSerialization/tests` | Golden-record tests plus harness unit tests. |
| `src/CoreWCF.DataContractSerialization.Generator/tests` | Generator unit tests. |

## Why golden records rather than round trips

A round-trip test - serialize, deserialize, compare objects - passes even if the generated
serializer and its deserializer are wrong in matching ways. It proves self-consistency, not wire
compatibility. Since the point of this project is that a generated serializer must be
indistinguishable on the wire from the reflection-based one, the fixtures record the **exact bytes**
the reference serializer produces, and comparison is byte equality with no canonicalisation.

Fixtures are therefore stored exactly as written: UTF-8, no byte-order mark, no XML declaration, no
indentation, no trailing newline, all on one line. `Fixtures/.gitattributes` marks them `-text` so
git's `text=auto` normalisation cannot rewrite a newline inside a serialized string value, and
`Fixtures/.editorconfig` stops an editor appending a final newline. Both would be silent
corruptions that pass on one operating system and fail on the other.

## Adding a corpus case

Register it in the catalog partial for the file that declares the type
(`TestCorpus/src/Catalog/CorpusCatalog.*.cs`):

```csharp
builder.Add<MyContract>("populated", () => new MyContract { Value = 1 })
       .WithKnownTypes(typeof(MyDerived))
       .WithTags("knowntype");
```

Then regenerate, **read the produced XML**, and commit the fixture alongside the registration so a
reviewer sees both in one diff.

Every `[DataContract]` type in the corpus assembly must be either registered or explicitly excluded
with `builder.Skip<T>("reason")` - `CorpusIntegrityTests` enforces it. That turns "what is not
covered yet" into a greppable list instead of tribal knowledge.

### Importing more upstream types

`TestCorpus/src/SerializationTestTypes/import.ps1` downloads a file at a pinned commit, checks the
licence header, injects the provenance block and reports any local `// CoreWCF:` modification the
overwrite would discard:

```powershell
cd src\CoreWCF.DataContractSerialization.TestCorpus\src\SerializationTestTypes
./import.ps1 -Sha <40-char-sha> -File InheritanceCases.cs -WhatIf   # drop -WhatIf to apply
```

Then build every target framework, add a `Catalog/CorpusCatalog.<file>.cs` registrar - remembering
its `static partial void` declaration **and** its call in the `CorpusCatalog` static constructor -
and regenerate. See `SerializationTestTypes/UPSTREAM.md` for what is imported, what is deferred and
why.

### Instances must be deterministic

The instance is part of the golden record's identity. Forbidden in a factory:

- `DateTime.Now`, `DateTime.Today`, `Guid.NewGuid()`, `new Random()`, anything reading machine state.
- `DateTimeKind.Local`. This one is the trap: it is serialized with the *machine's* UTC offset, so a
  fixture generated in Paris fails on a UTC build agent, and re-running locally never reveals it.
  Use `Utc` or `Unspecified`.

`FixtureWriterTests.EveryCase_CapturesRepeatably` catches within-process nondeterminism; it cannot
catch machine dependence, so the rule above is a review rule.

## Regenerating fixtures

Regeneration is opt-in, writes to the source tree, and **always fails the run**. That is deliberate:
a run that authors baselines must never be green, or leaving the variable set in CI would silently
rewrite the very oracle the build is checking. The reviewable artifact is `git diff`, not the test
result.

```powershell
# 1. The baseline framework first - it is the canonical record.
$env:COREWCF_REGENERATE_DCS_FIXTURES = '1'
dotnet test src\CoreWCF.DataContractSerialization\tests\CoreWCF.DataContractSerialization.Tests.csproj -c Debug -f net8.0 --filter "FullyQualifiedName~FixtureRegenerationTests"

# 2. Then the others, to record any framework-specific divergence.
foreach ($tfm in 'net9.0','net10.0','net472') {
    dotnet test src\CoreWCF.DataContractSerialization\tests\CoreWCF.DataContractSerialization.Tests.csproj -c Debug -f $tfm --filter "FullyQualifiedName~FixtureRegenerationTests"
}
Remove-Item Env:\COREWCF_REGENERATE_DCS_FIXTURES

# 3. Review.
git status --short src/CoreWCF.DataContractSerialization/tests/Fixtures
git diff src/CoreWCF.DataContractSerialization/tests/Fixtures
```

## Per-framework overrides

`net8.0` is the baseline: it is the lowest common target framework, present in every CI leg and in
the default local set, so a developer without a preview SDK still regenerates the canonical file.

Reads probe `Fixtures/<tfm>/<name>.xml` first, then `Fixtures/<name>.xml`. Writes on a non-baseline
framework compare against the baseline and record an override **only** where the bytes genuinely
differ - deleting any override that has become redundant. The override set therefore always equals
the true divergence set and prunes itself.

Divergence is real and currently affects three fixtures:

| Fixture | Framework | Cause |
| --- | --- | --- |
| `AllTypes`, `AllTypes2` | net472 | `double.Epsilon` renders as `4.94065645841247E-324` on .NET Framework and `5E-324` on .NET Core 3.0+, which made floating-point formatting shortest-round-trippable. |
| `DateTimeOnlyWrapper` | net10.0 | .NET 10 added native `DateOnly`/`TimeOnly` support to the serializer (dotnet/runtime#119835); earlier versions serialize them as ordinary structs. |

Note the divergence runs in both directions - older *and* newer than the baseline. Do not assume a
difference is a bug before checking which runtime changed.

## Adding a new target framework

Add a `<None Update="Fixtures\<tfm>\*.xml">` block to the test project. No code change is needed:
the running framework is read from an `AssemblyMetadataAttribute` injected by the project file
rather than from an `#if` ladder.

## The next milestone

Wiring the generator in should be one new class and one new test class:

```csharp
public sealed class GeneratedSerializerProvider : SerializerProvider
{
    public override string Id => "Generated";
    protected override DataContractSerializerOperationBehavior CreateBehavior(OperationDescription operation)
        => new GeneratedDataContractSerializerOperationBehavior(operation);
}

public sealed class GeneratedGoldenRecordTests : GoldenRecordTestsBase
{
    protected override SerializerProvider Provider { get; } = new GeneratedSerializerProvider();
}
```

The corpus, the fixtures and the test body are all reused unchanged. Two rules matter:

- **The generated provider must never set `CanProduceFixtures`.** A serializer that records its own
  expected output makes the conformance suite a tautology. `SerializerProviderTests` guards this.
- Use `TryGetUnsupportedReason` rather than deleting cases the generator cannot handle yet. The
  unsupported list then shows up in test output and shrinks visibly as the generator matures.

### Known design problem, deliberately left open

`XmlObjectSerializer`'s own members - all sixteen of them - carry `[RequiresDynamicCode]` and
`[RequiresUnreferencedCode]`; only its `protected` constructor is clean. IL2046 and IL3051 require
overrides to match their base annotations, and CoreWCF calls through the base-typed reference
anyway, so a reflection-free subclass makes serialization *work* under AOT without silencing a
single warning. The annotation lives in the abstraction, not the implementation.

Two candidate resolutions, to be decided when the real warning count can be measured:

1. **A parallel unannotated contract** - the System.Text.Json move. STJ never un-annotated
   `Serialize<T>(T)`; it added `Serialize(T, JsonTypeInfo<T>)` alongside. Here that means an
   attribute-free CoreWCF abstraction covering only the four members the formatter actually uses,
   plus a sibling factory returning `null` by default so the existing path still works. Generated
   types derive from both it and `XmlObjectSerializer`, the annotated overrides forwarding to
   unannotated implementations.
2. **Subclass `XmlObjectSerializer` alone** and accept that the warnings remain, treating "zero
   warnings" as a later milestone.

Putting `[UnconditionalSuppressMessage]` on CoreWCF's call sites is *not* an option: at that point
CoreWCF cannot know whether the instance is generated or a real reflection-based
`DataContractSerializer`, so the suppression would be unsound.

Also still open: `PrimitiveOperationFormatter` bypasses `CreateSerializer` entirely, fault details
are hard-typed to the concrete `DataContractSerializer` in `FaultContractInfo`, and `Message`,
`MessageFault`, `MessageHeader(s)`, `AddressHeader` and the WS-Trust/SCT security path all construct
one directly with no injection point. A warning-free CoreWCF needs all of these too.
