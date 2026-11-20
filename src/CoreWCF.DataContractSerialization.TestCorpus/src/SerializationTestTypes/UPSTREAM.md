# Imported from dotnet/runtime

Contract types in the `SerializationTestTypes` namespace are copied from the .NET runtime's own
`System.Runtime.Serialization.Xml` test suite. Reusing upstream types rather than inventing new ones
inherits years of accumulated edge-case coverage, and keeps our fixtures comparable with the values
upstream asserts.

**Pinned commit:** `bbfaee3bfa7edb0d556556bc32778d09a745134b` (2025-09-22)
**Upstream path:** `src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes/`
**Licence:** MIT, .NET Foundation - the same licence and header text CoreWCF uses, so files are
carried with their header unchanged.

Always pin a commit SHA, never `main`: an upstream edit to a populating constructor would otherwise
silently invalidate every affected fixture.

## Importing

Use [`import.ps1`](import.ps1). It downloads at a pinned commit, verifies the licence header,
injects the provenance block, writes CRLF/UTF-8-no-BOM, reports any local `// CoreWCF:`
modifications an overwrite would discard (leaving a `.bak`), and prints the table row below.

```powershell
./import.ps1 -Sha bbfaee3bfa7edb0d556556bc32778d09a745134b -File InheritanceCases.cs
```

Add `-WhatIf` to see what would change without writing.

## Files

| File | Upstream | Notes |
| --- | --- | --- |
| `Primitives.cs` | `SerializationTestTypes/Primitives.cs` | Imported in full; 37 contract declarations. |
| `InheritanceCases.cs` | `SerializationTestTypes/InheritanceCases.cs` | Imported in full; 33 contract declarations. The `IsReference` x inheritance matrix. |
| `InheritanceObjectRef.cs` | `SerializationTestTypes/InheritanceObjectRef.cs` | Imported in full; 10 contract declarations. The `BaseDC`/`DerivedDC` hierarchy the `TestInheritance` cases point at. |
| `_ImportSupport.cs` | extracted declarations | Four symbols the imported files reference but do not declare. |

Despite its name, `InheritanceObjectRef.cs` contains **no** `IObjectReference` or `ISerializable`
implementation - it is entirely inheritance hierarchies of plain data contracts, so it is imported
wholesale rather than cherry-picked.

### Extracted declarations

The imported files are not self-contained. Rather than pull in whole out-of-scope files, the
referenced declarations are reproduced in `_ImportSupport.cs`, verbatim and with per-symbol
provenance links:

- `IgnoreMemberAttribute` (from `ObjRefSample.cs`) - an empty marker upstream's `ComparisonHelper`
  uses to skip a member during object-graph comparison. It has no effect on serialization; this
  corpus compares XML bytes, not object graphs, so it exists purely so the imported files compile
  unmodified.
- `SimpleDC`, `SimpleDCWithRef` (from `ObjRefSample.cs`) - reference-preserving contracts used
  throughout `InheritanceCases.cs`. `ObjRefSample.cs` is not imported wholesale because it also
  declares `SerIser`, an `ISerializable` type out of scope for v1.
- `PublicDCStruct` (from `SampleTypes.cs`) - a nine-line struct used as a known type of `AllTypes`.

When either source file is eventually imported in full, delete the corresponding declaration from
`_ImportSupport.cs`.

## Local modifications

Every local edit carries a `// CoreWCF:` marker so a diff against upstream stays readable.

| Location | Change | Why |
| --- | --- | --- |
| `Primitives.cs`, `DateTimeOnlyWrapper` | Wrapped in `#if !NETFRAMEWORK` | `DateOnly`/`TimeOnly` do not exist on .NET Framework, and the corpus spans `$(TestTargetFrameworks)`, which still includes net472 on Windows. `CorpusCatalog.Primitives.cs` carries the same guard, so the case is excluded rather than silently dropped. |

No type was deleted on import. `InheritanceCases.cs` and `InheritanceObjectRef.cs` are unmodified.

## Types that cannot be golden-recorded

Some imported types are upstream *negative* tests - they exist so upstream can assert an exception.
They are registered with `builder.Skip<T>(reason)` rather than deleted:

- `DerivedWithIsRefTrue` sets `IsReference = true` under a base that leaves it false, which is an
  invalid contract; `DataContractSerializer` throws `InvalidDataContractException` instead of
  producing XML. The generator will need to reject it too, but that is a diagnostic test rather
  than a fixture.

## Deferred upstream files

| File | Size | Why not yet |
| --- | --- | --- |
| `SampleTypes.cs`, `DataContract.cs` | 244 KB | The coupled core; import together or not at all. |
| `DCRTypeLibrary.cs`, `DataContractResolverLibrary.cs`, `DCRSampleType.cs`, `DCRImplVariations.cs` | 72 KB | `DataContractResolver` is out of scope for v1. |
| `ObjRefSample.cs`, `SampleIObjectRef.cs`, `SelfRefAndCycles.cs` | 22 KB | `IObjectReference` and object-graph preservation are out of scope for v1. Three declarations are extracted from `ObjRefSample.cs` - see above. |
| `Collections.cs` | 12.5 KB | `[CollectionDataContract]` is a generator subsystem of its own; deferred deliberately. |
| `SerializationTypes.cs`, `SerializationTypes.RuntimeOnly.cs` | 173 KB | Different namespace; `RuntimeOnly` is reflection-only behaviour by name, the opposite of what a source generator targets. |
| `ComparisonHelper.cs` | 27 KB | **Never needed.** It compares object graphs; our oracle is XML bytes. |

## Re-syncing

1. Download the file at a new SHA and diff it against the copy here, ignoring the provenance header.
2. Re-apply any row from *Local modifications*.
3. Update the SHA above.
4. Regenerate fixtures and **read the diff**. An upstream change to a populating constructor is a
   legitimate fixture change; an unexplained one is a bug.
