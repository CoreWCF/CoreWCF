# AOT-safe source-generated DataContractSerializer — design notes and risk register

Working notes for the `CoreWCF.DataContractSerialization` package. Kept in the repo rather than in a
pull request description so the reasoning survives the work.

## Why

`DataContractSerializer` discovers types by reflection and every one of its members is annotated
`[RequiresDynamicCode]` + `[RequiresUnreferencedCode]`, so a CoreWCF service cannot publish cleanly
with `PublishAot` or `PublishTrimmed`. The fix is a Roslyn generator that reads `[DataContract]` and
`[DataMember]` at compile time and emits a reflection-free serializer per type.

It is an **optimization with a fallback**, not a replacement. The generated path is selected by an
`AppContext` switch; with the switch off, CoreWCF behaves exactly as it does today.

## Milestone map

| | State |
| --- | --- |
| **M1 — the oracle** | Done (`55e77dfe3`, `5f7757409`). 75 corpus cases whose exact serialized bytes are recorded from the real serializer, a golden-record harness where adding a second serializer is one subclass, and the package/generator/corpus skeleton. |
| **M2 — first generator slice** | Done. `WriteObject` over flat contracts, behind the switch, gated to net8.0+. 3 of 75 corpus cases byte-match; the rest report unsupported and skip. |
| **M3 — capability by capability** | Done for the corpus. Nested contract members and inheritance, enums, arrays and `List<T>` of primitives, `IsReference`, `[KnownType]`/`i:type`, `object` members, `[Serializable]`, `Uri`, `DateTimeOffset`, `XmlQualifiedName`, members declared as `ValueType`/`Enum`/`Array`, `Dictionary`/`ArrayList`, jagged arrays, and `DateOnly`/`TimeOnly`. **81 of 86** corpus cases byte-match; the five that skip are all deliberate exclusions. |
| **M4 — `ReadObject`** | Done for the corpus. **79 of 86** cases read back through generated code and reproduce their fixture when written out again by the reflection serializer. Five of the seven that skip have no generated serializer at all (the write-side exclusions); the other two have no accessible parameterless constructor, which generated code cannot work around. Read coverage is therefore write coverage minus exactly those two. |
| **M5+ — deferred** | The seam gaps below. Every case still skipping is a deliberate v1 exclusion or something a generator cannot reach: three contracts whose `[KnownType]` names a method resolved at run time, one with a non-public data member, one with no `[DataContract]` at all. `WriteObject` is feature-complete for the corpus. |

## What the switch does

`CoreWCF.Serialization.UseGeneratedDataContractSerializers`, resolved once into a `Lazy<bool>`:
explicit `AppContext` switch → assembly attribute emitted by the build targets → **default off,
except when `RuntimeFeature.IsDynamicCodeSupported` is false**, because under Native AOT the
reflection path is the broken one. This mirrors `Dispatcher/OperationInvokerBehavior.cs:18-42` and
`Dispatcher/InvokerUtil.cs:26-37`, which already gate the OperationInvoker generator the same way.

---

## The write algorithm, as it actually is

Recorded from `dotnet/runtime` at `bbfaee3bfa7edb0d556556bc32778d09a745134b`, under
`src/libraries/System.Private.DataContractSerialization/src/System/Runtime/Serialization/`. This is
reference material for reimplementation — nothing is copied. Where the generator implements one of
these rules it should cite the file in a comment.

### Member ordering — settled

`ClassDataContract.DataMemberComparer` (`ClassDataContract.cs:1476-1492`):

```csharp
int orderCompare = (int)(x.Order - y.Order);
if (orderCompare != 0) return orderCompare;
return string.CompareOrdinal(x.Name, y.Name);
```

So: **sort by `Order` ascending, then by `string.CompareOrdinal` on the *contract* name** (the
`Name=` override if present, otherwise the member name). Ordinal, not culture-aware.

`DataMemberAttribute._order` defaults to **-1**, and the setter throws `InvalidDataContractException`
for any negative value. Members without an explicit `Order` therefore always sort **before** every
ordered member, including `Order = 0` — a user cannot express -1. This closes an ambiguity the corpus
alone could not: it contains no case with an explicit `Order = 0`.

Base-class members come first because `ReflectionXmlClassWriter.ReflectionWriteMembers`
(`ReflectionXmlFormatWriter.cs:137-141`) recurses into `classContract.BaseClassContract` *before*
writing its own members. Ordering is per-contract, not across the whole flattened hierarchy.

### `EmitDefaultValue = false`

`ReflectionXmlFormatWriter.cs:160-176`. The member value is compared against the type's default; if
equal the element is skipped — **and if the member is also `IsRequired`, it throws** rather than
silently omitting. Worth an early diagnostic or a faithful runtime throw; silently omitting would be
a wire-level behaviour difference.

### Namespace prefixes are the writer's job, not the serializer's

The single most useful finding, and it removes most of the byte-exactness risk.

The serializer never allocates `a:`, `b:`, `d2p1:` itself. `ReflectionWriteStartElement`
(`ReflectionXmlFormatWriter.cs:206-218`) calls `WriteStartElement(nameLocal, namespaceLocal)` with no
prefix at all — the only exception is `XmlQualifiedName`, where `NeedsPrefix` forces
`Globals.ElementPrefix`. Prefix letters are assigned by `XmlDictionaryWriter` as namespaces come into
scope. **Emit the same calls in the same order and the prefixes fall out identically.**

The serializer explicitly declares a namespace in exactly two places:

1. **At the root**, via `XmlObjectSerializer.WriteRootElement` (`XmlObjectSerializer.cs:222-237`) —
   `contract.WriteRootElement(writer, name, ns)` followed by `writer.WriteNamespaceDecl(contract.Namespace)`
   when `CheckIfNeedsContractNsAtRoot` (`:240-255`) is true. That is true only when a root name was
   supplied explicitly (CoreWCF always supplies one, from the message part), the contract is not
   built-in, can contain references, is not `ISerializable`, and its namespace is non-empty and
   differs from the root namespace. This is the `xmlns:a="http://schemas.datacontract.org/2004/07/…"`
   seen on every fixture root.
2. **Per member**, via `classContract.ChildElementNamespaces[i + childElementIndex]` →
   `xmlWriter.WriteNamespaceDecl(...)` emitted *inside* the member's start element
   (`ReflectionXmlFormatWriter.cs:187-191`). This is why `xmlns:b` for collections, and `xmlns:z` in
   the nested-`IsReference` case, appear on the member element rather than the root.

`z:Id` / `z:Ref` are written as attributes with an explicit `Globals.SerPrefix`
(`XmlObjectSerializerWriteContext.cs:218,224`), which is why `z:` is stable rather than allocated.

### `[Serializable]`, and why it is not the same as an implicit contract

From the `else` branch of `hasDataContract` in `ClassDataContract.ImportDataMembers`:

- `[DataContract]` **wins** when a type carries both. `BaseSerializable` in the corpus is exactly
  that shape and is written from its `[DataMember]`s, not its fields.
- Otherwise every instance **field** takes part — public and non-public — excluding `[NonSerialized]`.
  Properties never do, at all.
- Each field keeps its own name, and the fields sort by the same `DataMemberComparer`. Serializable
  fields get `Order = 0` rather than the `-1` an unspecified `[DataMember]` gets; within a single
  contract they are all equal either way, so the sort reduces to ordinal by name.

This is not the implicit no-attribute contract that v1 excludes. `[Serializable]` is an explicit
opt-in with a defined member set; a bare POCO is inferred, and stays out of scope.

Two restrictions the generator adds, both to avoid claiming more than it can deliver:

- **Only types declared in source.** `[Serializable]` is everywhere in the framework — `Uri`,
  `ArrayList` and `Dictionary<,>` all carry it — and their wire format has nothing to do with their
  field layout. A type from metadata keeps whatever answer it had before.
- **Not `ISerializable`.** That takes over serialization entirely; it is a different write algorithm,
  not a different member list, and the fields are not what would go on the wire.

Non-public fields still decline, for the reason they always did: generated code lives in the
context's class and cannot reach another type's privates, however close by they are compiled.

### `DateOnly` and `TimeOnly` — the one format decided by the runtime

Every other rule here is a property of the contract. These two are a property of the **runtime**:

- Up to **.NET 9** the serializer does not recognise them and writes a contract with no members —
  an empty element that drops the value entirely, with the `System` namespace declared on it.
- **.NET 10** writes them as primitives: `yyyy-MM-dd` for a date, `HH:mm:ss.FFFFFFF` for a time
  (optional fractional digits, so trailing zeros and the dot are omitted), and no namespace
  declaration at all.

It is genuinely runtime-determined and not target-framework-determined, which was **verified rather
than assumed**: running the net8.0 test assembly under `DOTNET_ROLL_FORWARD=LatestMajor` makes the
reflection serializer produce the .NET 10 format. So the generator emits a runtime test
(`Environment.Version.Major >= 10`) rather than deciding at compile time.

That distinction has teeth. With a compile-time decision, a net8.0 assembly rolled forward onto
.NET 10 would emit the old format while the reflection path emitted the new one — and the harness
would not have caught it, because the generated provider would have kept matching its own stale
baseline. With the runtime test, the same roll-forward run fails **both** providers on exactly the
same two cases, which is the fixtures being keyed to the compile-time framework and not a defect.

The upstream `DateTimeOnlyWrapper` only ever holds default values, so it cannot tell a dropped value
from a zero one. `SanityDateAndTimeOnly` carries real ones, which is what makes the pre-.NET 10 data
loss visible in the fixture: a `DateOnly` of 2020-01-02 records as `<a:Date/>`.

### Object identity, as `IsReference` defines it

From `XmlObjectSerializerWriteContext.OnHandleIsReference` and `ObjectToIdCache`:

- Ids come from a **per-call** cache whose counter starts at **1**, so every document restarts at
  `i1`. Only objects whose contract is `IsReference` consume an id — which is why a
  reference-preserving member of a plain root is `i1` and not `i2`.
- Lookup is by **reference**, via `RuntimeHelpers.GetHashCode` and `==`, never `Equals`. A contract
  that overrides equality still gets one id per instance, and two equal-but-distinct instances must
  not collapse into a single `z:Ref`.
- First sight writes `z:Id` and the members; a later sight writes `z:Ref` and **nothing else** —
  `OnHandleIsReference` returning true means "already written". That, not a visited-set check, is
  what terminates a cycle.
- The decision belongs to the element that wraps the object, so it is taken once at the root or on
  the member element — never once per level of a base chain.
- `IsReference` is inherited, and a derived contract may restate it but not contradict its base:
  `ClassDataContractCriticalHelper.EnsureIsReferenceImported` throws `InvalidDataContractException`.
  It is also invalid on a value type. The generator declines both rather than emitting a serializer
  that would accept a contract the real one rejects.

The `z` prefix is never declared by the generator. Writing an attribute in the serialization
namespace makes the writer declare it wherever it first comes into scope, which reproduces both
fixture shapes on its own: `xmlns:z` on the root when the root contract is `IsReference`, and on the
member element when only a nested contract is.

---

## The read algorithm, as it actually is

`ReadObject` is not the write algorithm run backwards. Two rules differ, and both come from
dotnet/runtime rather than from the fixtures.

### Members are flattened base-first, not read one level at a time

The writer recurses, one call per level of the base chain, because writing is unconditional.
`ReflectionGetMembers` shows the reader cannot:

```csharp
protected static int ReflectionGetMembers(ClassDataContract classContract, DataMember[] members)
{
    int memberCount = (classContract.BaseClassContract == null) ? 0 :
        ReflectionGetMembers(classContract.BaseClassContract, members);
    ...
}
```

Base members fill the array first, then the derived ones, and `ReflectionReadMembers` is a single
loop over that flat array with one monotonically advancing index. A per-level loop would let the
base's own loop reach a derived member, fail to recognise it, and skip it in silence - the members
would vanish and the read would report success.

`ClassDataContract` builds the matching namespace array by copying rather than recomputing:

```csharp
Array.Copy(BaseClassContract.MemberNamespaces!, MemberNamespaces, baseMemberCount);
...
MemberNamespaces[i + baseMemberCount] = Namespace;
```

So **an inherited member is matched against the namespace of the contract that declares it**, not
the derived contract's. The generator flattens the same way, in `FlattenedMembers`.

### An enum comes back through its name table, never through Enum.Parse

`EnumDataContract.ReadEnumValue` matches the wire text against the contract's own names, ordinally,
and throws on one it does not recognise. Two details are load-bearing:

- **A flags enum is a space-separated list, and an empty one is legal** - it is how a zero value is
  written when no member names it. A non-flags enum is a single name, and there the empty string is
  an error rather than zero.
- **A name must match in full.** Comparing only the first *count* characters would accept a
  truncated name and return the wrong member, so the generated lookup tests the length first.

An unrecognised name always throws rather than falling back to a numeric parse. `Enum.Parse` would
accept names the contract never declared and numbers the contract never wrote, which is precisely
the silent-wrong-graph failure this project exists to prevent. The ulong-backed cast reinterprets
bits rather than converting them, matching what the writer does and what upstream does through
`Enum.ToObject` on the unsigned value.

### An empty collection and a null one are different documents

An empty element yields an empty collection; only `i:nil` yields null. Getting that backwards is
invisible until something round-trips, which is what the read oracle is for: it reads with the
generated serializer and writes back with the **reflection** one, so any difference in the recovered
graph shows up as a byte difference against the recorded fixture.

The same three-way distinction applies to a dictionary, and one populated `Dictionary<string,
string>` reaches none of it - which is why `SanityDictionaries` was added rather than trusting the
code. Its fixture pins an empty map, a missing one, an entry whose `Value` carries `i:nil`, a
base64 value and a non-string key, all in one document. A branch no fixture exercises is a branch
that is not verified, however carefully it was written.

### Three kinds are not read from text at all

A byte array is base64 the reader decodes itself. The other two are worth stating, because neither
is what it looks like:

- **`DateTimeOffset` is a two-member contract, not a value.** The inverse follows
  `DateTimeOffsetAdapter.GetDateTimeOffset`: an Unspecified `DateTime` is *paired* with the offset,
  anything else is *converted* to it. The writer recorded `UtcDateTime` and the offset separately
  rather than a local time, so treating both cases alike would shift every value carrying an offset
  by that offset.
- **A `QName` has to resolve a prefix against an element that is still open.** The prefix is declared
  on the member element and nowhere else, so reading the text and the end tag in one call -
  `ReadElementContentAsString` - pops the scope that defines it and leaves nothing to resolve
  against. `XmlReaderDelegator.ReadElementContentAsQName` splits the read into start, content, end
  for exactly this reason, and the generated reader splits it the same way.

The empty `QName` is a third shape again: the writer emits no content for it, so it comes back as an
empty element rather than as an empty string that happens to parse the same way.

#### The prefix a null QName does not get

`SanityQualifiedNames` was added for the read side and immediately failed on the *write* side, which
is the useful kind of failure. A non-null `XmlQualifiedName` member element carries a prefix of its
own - `NeedsPrefix` in `ReflectionXmlFormatWriter` forces one for this type alone - but a **null**
one does not: the prefix belongs to the path that writes a value, and a null member is written by
`WriteNull`, which opens the element with whatever prefix is already bound.

The generator had been applying the prefix unconditionally. `AllTypes` never caught it because its
`XmlQualifiedName` is non-null, so the two halves of the rule are now pinned by two different
fixtures. Nothing about this is visible to a semantic XML diff, which is why the harness compares
bytes.

### `DateOnly` and `TimeOnly` are read by whichever rule the runtime wrote them under

The one format decided by the runtime rather than by the contract, so the reader carries the same
`Environment.Version.Major >= 10` test the writer does.

Before .NET 10 the serializer does not know what these types are and writes a contract with no
members - an empty element that drops the value. Reading `default` there is not a fallback: it is
what the recorded document says, and it is what the reflection-based reader produces from the same
bytes, which is what keeps the round-trip exact. On .NET 10 they are primitives and the text is
parsed.

Upstream is worth following closely here, because the two are not symmetric:

```csharp
// ReadElementContentAsDateOnly
return DateOnly.ParseExact(s, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo,
    DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite);

// ReadElementContentAsTimeOnly
var dto = XmlConvert.ToDateTimeOffset(s);
return TimeOnly.FromTimeSpan(dto.TimeOfDay);
```

`XmlReaderDelegator` also defines a `ParseTimeOnly` that mirrors the DateOnly one - and never calls
it. Copying that helper instead of the code actually on the path would have narrowed what the reader
accepts, rejecting a `Z` or an offset the real one takes.

Because the branch is a run-time test rather than a compile-time one, **one test verifies both
halves**: `SanityDateAndTimeOnly` round-trips on net8.0 and net9.0 against the lost-value fixture and
on net10.0 against the primitive one.

### Resolving an `i:type` is not the inverse of announcing one

Announcing is a switch on the runtime type. Resolving is a lookup by name, and the candidate set is
the compile-time known-types closure - so what the serializer does through reflection is a chain of
string comparisons in generated code. Following
`XmlObjectSerializerReadContext`:

- **No `i:type` means the declared contract.** The writer omits it for the declared type, so the
  absent name and the declared name share one branch.
- **A name that resolves to nothing throws**, as `DcTypeNotFoundOnDeserialize` does. Falling back to
  the declared contract would produce an instance missing every member the derived one adds, and
  report success.
- The attribute is read while the reader still sits on the element, because the prefix it uses is
  declared there - the same constraint `QName` has.

#### The root decides for itself, in both directions

A root has no member to carry the decision, so it makes it itself. Adding the read side exposed that
the **write** side never had one: `WriteObject` cast the graph to the declared contract, so a service
returning a derived instance through a base-typed contract wrote only the base members, with no
`i:type` and no error. That is the silent-data-loss failure this project exists to prevent, and it
was present on the write path independently of any of the read work.

`SanityBase.derived-instance` now pins it. The recorded document puts `i:type` ahead of the namespace
declarations and reuses the prefix already bound to the contract namespace rather than declaring a
second one - a member element declares its own, a root does not:

```xml
<Root i:type="a:SanityFurtherDerived" xmlns="http://tempuri.org/" xmlns:a="..." xmlns:i="...">
```

`SanityPolymorphic` covers the member side with all four shapes at once, because a single derived
instance cannot tell "resolved the name" from "took the only branch": a declared instance carrying no
`i:type`, two different derived types carrying different ones, and a null carrying none.

### A boxed member needs a second table, keyed by XSD name

A polymorphic member's candidates are all contracts, so resolving a name means finding a contract. A
boxed member's may be a primitive announced by an XSD name with **no contract behind it**, so it needs
the inverse of the writer's `BoxedPrimitives` table. Three things that table settles, and that the
inverse has to preserve:

- `"byte"` is `sbyte` and `"unsignedByte"` is `byte` - the pair most likely to be quietly "corrected"
  into agreeing with the CLR names.
- `char`, `Guid` and `TimeSpan` are named in the serialization namespace, because XML Schema has
  nothing to call them.
- A **bare `object`** carries neither `i:type` nor content, so there is nothing to recover but the
  fact that something was there.

What the declared type buys is a check. A member declared `ValueType` cannot hold a string, so an
`i:type` naming one is refused with a `SerializationException` rather than surfacing later as an
`InvalidCastException` out of generated code. `Enum` and `Array` members admit no primitive at all,
so an unmatched name there never reaches the XSD table.

`Array` is the one shape that is not a value: the only array the writer emits is `object[]`, and it
announces no `i:type` at all - each item carries its own. Reading it is the same loop the untyped
item reader serves for an `ArrayList`.

### One sequence loop, three containers

Reading a sequence is the same loop every time - fill a container from the children of an element,
then assign it - and the containers are what differ:

| | Container | Finish |
| --- | --- | --- |
| `T[]` | `List<T>` | `.ToArray()` |
| `List<T>` | `List<T>` | assign as is |
| `ArrayList` | `ArrayList` | assign as is |

`ArrayList` is the one a read cannot infer from the items, which is why the spec records it
explicitly: writing one needs nothing but `foreach`, so the write side never had to know.

A jagged collection is that loop nested. The `ArrayOf` element is the inner collection's own element
- there is no second wrapper - so an item read is the same loop one level down, over the innermost
items. A null inner row is `i:nil` like any other missing reference, which is what makes
`{ {1,2}, {}, null }` three different documents rather than two.

#### A contract with no parameterless constructor can be written and never read

`DataContractSerializer` allocates without running a constructor. Generated code has no such option,
so `ArrayContainer` - which declares `ArrayContainer(bool)` and thereby suppresses the implicit
parameterless one - and `PrivateCstor` are written byte-exactly and never read. This is not a gap to
close later; it is the one place where the generated path is strictly less capable than reflection,
and it fails by declining rather than by producing a wrong graph.

That is also why `SanityUntypedCollections` exists: the upstream cases covering these containers are
exactly the two that cannot be read, so without it the container code would have shipped with no
fixture exercising it.

### `IsReference` needs a table, not a fixup pass

This was written down wrong for several milestones, so it is worth stating plainly: **a `z:Ref`
never points forward.** The writer assigns an id the first time it *writes* an instance, so the
element carrying `z:Id` is written - and therefore read - before any reference to it. There is
nothing to defer.

Upstream is unambiguous. `GetExistingObject` throws `DeserializedObjectWithIdNotFound` on an id it
has not seen rather than recording it for later, and the class comment says so directly: *"BinaryFormatter
supports this by fixing up such references later. These XmlObjectSerializer implementations do not
currently support fix-ups. Hence we throw."*

What makes a **cycle** work is not patching up afterwards but *when* the instance is recorded:

```csharp
__typed = new Node();
scope.Add(reader.GetAttribute("Id", ...), __typed);   // before, not after
// ... now read the members, one of which may be a z:Ref back to __typed
```

An instance inside its own graph is referred to while it is still being filled in - that is what a
cycle *is* - so recording it after its members would turn that reference into a lookup that fails.
Upstream calls `AddNewObject` before deserializing members for the same reason.

The reader's scope is much the smaller half of the pair: one `Dictionary<string, object>`, allocated
on first use, one per `ReadObject` call so nothing survives between documents. The writer's scope has
to assign ids, track by reference identity, and guard by-value recursion; the reader's only has to
remember.

`SanityReferenceNode.cycle` proves it byte-for-byte - writing the recovered graph back out reproduces
the same `z:Id`/`z:Ref` pattern, which only happens if the identities match - and
`CyclicGraphTests.GeneratedReader_RecoversACycleRatherThanACopy` states the same claim directly,
since a byte comparison standing in for an object-identity claim is worth backing up once.

### What stays unreadable, and why

A contract is readable only if every contract it reaches is - it is a graph question, computed with
memoisation, where a contract that reaches itself is assumed readable because the recursion
terminates at run time on a nil or empty element rather than statically.

| Not read | Reason |
| --- | --- |
| Contracts with no accessible parameterless constructor | `DataContractSerializer` allocates without running a constructor. Generated code has no such option, so these can be written and not read. |

---

## Falling back is visible, and still a fallback

Both halves matter, and they are not in tension:

- **The fallback stays.** `GetSerializer` returns null, `CanReadObject` stays false, CoreWCF uses the
  reflection-based `DataContractSerializer`, and nothing breaks. That is what makes migrating
  contract by contract possible.
- **It is no longer silent.** A contract that falls back produces a build warning naming the reason.

The second half is a change of mind, and worth recording as one. While the generated path was purely
an optimization, silence was right: nothing was lost but speed. It is *not* only an optimization
under Native AOT - the switch defaults on there precisely because the reflection path is the broken
one - so a silent fallback is a build that looks clean and throws at run time.

| Id | Severity | Fires when |
| --- | --- | --- |
| `COREWCF_0403` | Warning | No serializer was generated for a listed contract. |
| `COREWCF_0404` | Warning | A listed contract is written by generated code but not read by it. |

Three decisions inside that:

- **Warning, not error.** The fallback is correct behaviour, and a build that fails on it would make
  step-by-step migration impossible. `NoWarn` and EditorConfig work as usual for anyone who has
  looked at one and accepted it.
- **Reported on the type the user listed**, not on every contract reachable from it. A nested
  contract that cannot be written makes its container unsupported too, so reporting both would bury
  the one line they can act on under a cascade of consequences. The reason text carries the cause:
  *"member 'Value' cannot be read back: App.Inner cannot be read back: member '_hidden' is not
  public"*.
- **Two ids, not one.** Half a fallback is a different situation: a service that only *returns* a
  contract is unaffected by its read half, so the two are worth telling apart at a glance.

The read-side answer is computed by the emitter rather than the parser, because readability is a
property of the whole contract graph rather than of one contract - which is also why `IsReadable`
became a function returning a *reason* rather than a bool. "It cannot be read" is not something
anyone can act on.

---

## The premise, executed

Everything above is verified under a normal runtime, where dynamic code is available and nothing is
trimmed. `src/CoreWCF.DataContractSerialization.AotSmokeTest` publishes a CoreWCF service with
`PublishAot` and calls it over HTTP, so the premise the work exists for is run rather than assumed.
It is not part of `dotnet test`: it proves something only once published. Its README says how.

**It passes.** The generated serializer writes and reads the graph with `IsDynamicCodeSupported`
false, and a whole service answers over HTTP with contents that match.

Three things it found that were not visible from the unit tests.

### The serializer this replaces loses data silently, not loudly

Given the same contract in the same binary, the reflection-based `DataContractSerializer` fails - and
*how* it fails is the finding. Before the contract types were rooted with `[DynamicDependency]` it
did not throw: it wrote a document a quarter of the size, a graph missing most of its members,
returned as though nothing were wrong. With the types rooted it throws `NullReferenceException`
instead.

Silent truncation is the worse of the two and it is what an AOT app would have got, which is the
concrete form of the argument for warning on a fallback rather than falling back quietly.

### CoreWCF needs the contract rooted before the trimmer will keep it

`TypeLoader` finds operations by reflecting over the contract interface, and nothing calls those
methods statically - the whole point of a dispatcher is that the call is dynamic. Without a
`[DynamicDependency]` the interface arrives with **zero operations** and the host refuses to start.
That is an annotation gap in CoreWCF, not something an application should have to know; the smoke
test works around it so the stages after it can be reached.

### A warning-free publish is a long way off

339 trim and AOT warnings, essentially all from CoreWCF rather than from this area: 207 IL3050, 72
IL2026, 56 assorted reflection-pattern warnings, and 4 IL3054 for generic recursion aborted in the
message filter tables - that last one throws if the path is ever reached. They come from the security
stack, the channel proxy and the dispatcher.

So a CoreWCF service *runs* under AOT for this shape, without the guarantees a clean publish would
give. Serialization is one of those warnings addressed. The rest are risk 5.

---

## Risk register

Ordered by how much each threatens the milestone.

### 0. What the first slice actually proved

Reading the algorithm before writing the emitter worked: the first three cases matched byte for byte
on the first run, with no iteration on prefixes at all. The two findings that carried it were that
prefix allocation belongs to the writer, and that primitive formatting is `writer.WriteValue` with
exactly three exceptions (`char` as a number, `Guid`/`TimeSpan` via `WriteRaw`, `byte[]` as base64).

Two build-level traps cost more time than the serialization did, and are worth remembering:

- **The shipped `.targets` cannot be imported from a project body.** NuGet injects it after the SDK
  targets, where `TargetFrameworkIdentifier` is populated; an import in the body evaluates earlier,
  silently sees an empty value, and disables the generator with no error. In-repo consumers must
  mirror the gate conditioned on `$(TargetFramework)` instead.
- **`EmitCompilerGeneratedFiles` poisons the next build.** It writes under the project directory
  where the default glob compiles it as ordinary source, so every generated member is then declared
  twice. The test project excludes `generated/**`. Note also that cleanup can fail *silently* on
  these paths - they exceed `MAX_PATH`, and PowerShell's `Remove-Item` swallows it.

### 1. Byte-exactness — largely mitigated, not eliminated

The harness compares bytes with no canonicalisation, deliberately: a semantic XML diff would call
differently-prefixed but structurally identical documents equal, and wire compatibility is the whole
point. Reading the upstream algorithm converts most of this from reverse-engineering into porting,
and the finding that prefix allocation belongs to the writer removes the hardest part.

What remains: the emitted code must make the *same writer calls in the same order*. Any deviation —
an extra `WriteNamespaceDecl`, a differently-timed `WriteStartElement` — changes prefixes downstream.

### 2. ~~"Flat contracts" is a smaller slice than it sounds~~ — closed by M3

Roughly 10 of the 75 corpus cases. "Flat" does not mean "no nested types": `SanityCustomNaming` has a
contract-typed member, so the generator must already walk the transitive type graph and emit a
serializer per reachable contract. The milestone proves the loop end to end; it does not cover most
of the corpus.

M3 took it from 3 to 49 of 76 by adding one capability at a time. The prediction held: the
transitive-graph machinery built for the first slice is what everything since has been layered onto.

### 3. ~~Member ordering is under-determined~~ — closed

Settled above from `ClassDataContract.cs`. Add corpus cases pinning `Order = 0` and an ordinal-vs-
culture-sensitive name pair, so the oracle records the answer rather than only this document.

### 4. ~~Generated code must compile on net472~~ — closed by design

The generator is gated to net8.0+ by `CoreWCF.DataContractSerialization.targets`, so emitted code
never faces a net472 compiler and may use LangVersion 11. `DataContractSerializerContext.GetSerializer`
is `virtual` returning `null` rather than `abstract` so a user's `partial` context still compiles on
net472, where the generator never runs — falling back to reflection with no `#if` in user code.

Residual: on net472 the generated tests must report every case *unsupported*, not silently pass. A
pass because nothing ran looks identical to a pass because everything matched.

### 1a. A corpus case only tests the generator if the context lists its type

`SanityPrimitiveArrays` was added with the collections work and its fixture recorded, but the type
was never added to `GeneratedCorpusContext`. `GetSerializer` therefore returned null for it and
`GeneratedGoldenRecordTests` skipped it, with a skip reason that read exactly like every legitimately
unsupported case. The collections work looked verified and was not.

Worse, when the type was finally registered the generated code **did not compile**: `ulong` members
emitted `writer.WriteValue(value)`, which is ambiguous rather than missing, because ulong converts
implicitly to float, double and decimal and to none of them better than the others. Upstream avoids
it in `XmlWriterDelegator.WriteUnsignedLong` by going through `WriteRaw(XmlConvert.ToString(value))`
- a fourth `WriteRaw` case alongside Guid and TimeSpan.

Two things to keep in mind, neither of which the harness can currently catch on its own:

- **Registering a type in the corpus catalog is half the job.** Adding it to the context is the other
  half, and nothing fails if it is forgotten. A test asserting that every catalogued case's contract
  type appears in the context would close this, and should be written.
- **A compile error in generated code can present as csc exiting 1 with no diagnostic at all.**
  Building with `-p:EmitCompilerGeneratedFiles=true` made the same compilation succeed, which is what
  made the failure look like a compiler crash rather than a bug in the emitted source. When csc dies
  silently, dump the generated file and compile it as an ordinary source to get the real error.

### 4a. ~~A cycle without `IsReference` overflows the stack instead of throwing~~ — closed

`DataContractSerializer` counts nesting depth and, past 512 levels, checks whether the object is
already on the path and throws `CannotSerializeObjectWithCycles`
(`XmlObjectSerializerWriteContext.OnHandleReference`). The generator now carries the same counter,
in the per-call scope that already tracks `z:Id`, and wraps every by-value contract write in
`EnterByValue`/`ExitByValue`. Reference-preserving contracts are left unguarded on purpose: the
second sight of an instance is a `z:Ref` with no content, so they cannot recurse forever.

The golden-record corpus cannot cover this — the real serializer refuses such a graph, so there is
no output to record — so `CyclicGraphTests` covers it directly instead: both paths must throw
`SerializationException` for the same cyclic graph, and a 600-deep chain must still come out
byte-identical, which is what stops the guard mistaking depth for a cycle.

One difference from upstream worth knowing: the 512 is counted in nested contracts here and in
XML writer depth there, so the exact level at which the throw happens can differ. That is not
observable in a valid document, where neither throws at all.

### 4c. ~~`AllTypes` needs eight capabilities, not one~~ — closed, but the reporting lesson stands

`ParseContract` records the **first** reason a contract is declined (`unsupportedReason ??=`) and
stops describing it. For a wide contract that reads like a single remaining blocker when it is one
of many, and it caused exactly that misreading of `AllTypes` once already.

`AllTypes` and `AllTypes2` between them still need:

| Member | Capability | State |
| --- | --- | --- |
| `object z5` + `[KnownType]` | enums in an `object` member | done |
| `MyEnum1[] enumArrayData` | collections of enums | done |
| `Uri uri` | `Uri`, written via `GetComponents(SerializationInfoString, UriEscaped)` | done |
| `List<DateTimeOffset> lDTO`, `DateTimeOffset? nDTO` | `DateTimeOffset` as a two-member contract in the `System` namespace | done |
| `XmlQualifiedName xmlQualifiedName` | QName values, whose member element takes a `q:` prefix of its own rather than reusing the contract's | done |
| `ValueType timeSpan`, `ValueType valType` | a member declared as `System.ValueType`: the boxed switch again, but over value types | done |
| `Enum enumBase1` | a member declared as `System.Enum`: the boxed switch over enums | done |
| `Array array1` | a member declared as `System.Array`, whose items declare the Arrays namespace as a *default* xmlns on each element rather than a prefix on the member | done |

All eight landed and both contracts now byte-match. Two of the eight were only ever going to matter
here - `XmlQualifiedName`'s own element prefix and the `System.Array` member - and both are
implemented narrowly: the array writer covers `object[]` of primitives and bare objects and throws
otherwise, because a contract or enum inside an untyped array would need the containing contract's
known types, which an item writer shared across the whole context does not have.

The reporting lesson is now fixed rather than only noted: `ParseContract` used to record the **first**
reason a contract was declined and stop, so a wide contract read like a single blocker when it was
one of many — which misled a coverage estimate once. It now collects every reason, and the emitted
report prints one line per reason. `DateTimeOnlyWrapper` immediately went from one line to four.

The general lesson is about the report, not the contract: a coverage report that names one cause per
contract will understate the work whenever the contract is wide. Collecting every reason rather than
the first would make the remaining effort legible, and is worth doing before planning the next slice.

### 4b. An `object` member holding a collection throws rather than falling back

The generated switch for an `object` member covers the boxed primitives and whatever `[KnownType]`
names. DataContractSerializer is wider than that: arrays and collections are always allowed in an
object member without being declared. A graph that puts one there gets a `SerializationException`
from the generated path where the reflection path succeeds.

It cannot be a fallback: by the time the runtime type is known the member element is already open.
Closing it means writing collections into an object member, which needs the collection contract
naming rules the generator does not implement yet. `AllTypes.array1` is the corpus case waiting on
it, though that contract is blocked on several other things too.

### 5a. ~~`ReadObject` is built but not yet called~~ - closed

`PartInfo` now prefers the generated serializer for reading as well as writing, gated on
`CanReadObject` so the two directions stay independent.

Two things were needed rather than one. `IsStartObject` was being asked of `part.Serializer` at three
call sites, which constructs the reflection-based serializer - the thing the generated path exists to
avoid - so it moved onto `PartInfo` too. Both now resolve through a single `ReadingSerializer`
property, because they have to agree: a part recognised by one serializer and then read by the other
is a part read by something that does not recognise it.

The seam had no test in either direction before this - the switch defaults off, so nothing in the
suite had ever taken the generated branch. `PartInfoSerializerSelectionTests` covers it by planting a
recording serializer on the part rather than by turning the switch on, since the switch resolves into
a process-wide `Lazy` that would leak into a concurrent suite, and its own resolution order is
already covered elsewhere.

### 5. The seam has gaps this work does not close

`CreateSerializer` is not a complete injection point. Even with a perfect generator, CoreWCF still
constructs a reflection-based `DataContractSerializer` directly in:

- `Dispatcher/PrimitiveOperationFormatter` — selected instead of the DataContract formatter for
  simple contracts, bypassing `CreateSerializer` entirely.
- `Dispatcher/FaultContractInfo.cs:42-58` — fault details, hard-typed to the concrete class.
- `Channels/Message.cs`, `MessageFault.cs`, `MessageHeader.cs`, `MessageHeaders.cs`,
  `AddressHeader.cs` — no injection point at all.
- The WS-Trust / secure-conversation token serializers under `Security/`.

An AOT smoke app will still warn until these are addressed. This work covers the operation body path.

### 6. Public API permanence

`AotXmlObjectSerializer` and the context types become public API on first release. Verified there is
**no** public-API baseline or approval test in this repo, so nothing enforces or records the surface —
which cuts both ways: no baseline to update, and no guard against accidental changes later.

### 7. `Lazy<bool>` caches on first read

A test that sets the switch after the first serializer is created silently gets the old value and the
generated path never runs — presenting as a **pass**, not a skip. Set it once per process, and have
the generated provider assert the switch took effect rather than trust it.

### 8. Incremental generator caching

The BuildTools generators hold raw `ISymbol` references in their specs and build them inside
`RegisterSourceOutput`, which defeats caching. This generator keeps symbols out of the specs and
builds them in `transform` before `Collect()`. Getting it wrong costs build time silently rather than
failing a test, and nothing verifies it.

---

## Open questions

- Naming: `AotXmlObjectSerializer` is a placeholder. It is public API, so worth settling before release.
- ~~Should an unsupported contract shape be a build-time diagnostic, or a silent fallback?~~
  Settled: both. The fallback happens and a warning names it - see "Falling back is visible, and
  still a fallback" above.
- ~~**Nothing has ever been published with `PublishAot`.**~~ Settled by
  `CoreWCF.DataContractSerialization.AotSmokeTest` - see "The premise, executed" above.
- Where the context must live once a corpus type has a private `[DataMember]`. Today exactly one does
  (`BaseDCNoIsRef._data`, out of slice), so the context sits in the test project and generator bugs
  cannot break the corpus build that the reflection oracle depends on. That trade reverses the moment
  an in-slice case needs private member access.
