# AOT smoke test

Publishes a CoreWCF service with Native AOT and calls it over HTTP, so the premise the generated
serializer exists for is executed rather than assumed. Everything else in this area is verified under
a normal runtime, where dynamic code is available and nothing is trimmed.

Not run by `dotnet test`. It proves something only once published, which is a separate command.

## Running it

```powershell
dotnet publish src\CoreWCF.DataContractSerialization.AotSmokeTest\src\CoreWCF.DataContractSerialization.AotSmokeTest.csproj `
    -c Release -r win-x64

.\bin\Release\CoreWCF.DataContractSerialization.AotSmokeTest\net10.0\win-x64\publish\CoreWCF.DataContractSerialization.AotSmokeTest.exe
```

Exit code 0 is a pass. On Windows the publish needs the MSVC linker, and `vswhere.exe` must be on
`PATH` for the ILCompiler to find it - a Developer Command Prompt has both, or add
`C:\Program Files (x86)\Microsoft Visual Studio\Installer`.

## What each stage answers

| Stage | Question |
| --- | --- |
| runtime is AOT | Is this actually an AOT publish? Without it, nothing below is a test of one. |
| switch is left at its default | Is the generated path selected without anyone opting in? |
| generated serializer resolves and round-trips | Does generated code write and read a graph with no dynamic code and after trimming? |
| service answers over HTTP | Does a whole CoreWCF service work, end to end, published AOT? |
| reflection serializer does not silently truncate | What does the serializer this replaces do here? |

The stages are separate on purpose. Serialization is the last link in a long chain, and a failure in
the host or the dispatcher would otherwise read as "AOT does not work" without saying what does.

The client is a raw `HttpClient` posting a hand-written envelope. `System.ServiceModel` would be the
obvious choice and is not an option: it does not support AOT either, so a failure there would say
nothing about the service.

## What it found

**The generated serializer works.** It writes and reads the graph under AOT, and the service answers
over HTTP with a document whose contents match.

**The serializer it replaces does not.** Given the same contract in the same binary, the
reflection-based `DataContractSerializer` fails - and how it fails is worth knowing. Before the
contract types were rooted with `[DynamicDependency]` it did not throw at all: it wrote a document a
quarter of the size, a graph missing most of its members, returned as though nothing were wrong. With
the types rooted it throws `NullReferenceException` instead. Silent truncation is the worse of the
two, and it is what an AOT app would have got.

**CoreWCF needs the contract rooted for the trimmer.** `TypeLoader` finds operations by reflecting
over the contract interface, and nothing calls those methods statically - the whole point of a
dispatcher is that the call is dynamic. Without `[DynamicDependency]` on the contract the interface
arrives with zero operations and the host refuses to start. That is an annotation gap in CoreWCF
rather than something an application should have to know.

**The publish reports 339 trim and AOT warnings**, essentially all from CoreWCF rather than from this
area:

| Rule | Count | What it is |
| --- | --- | --- |
| IL3050 | 207 | a call needing dynamic code |
| IL2026 | 72 | a call needing types that cannot be statically analysed |
| IL2075, IL2070, IL2072, ... | 56 | reflection over types the trimmer cannot follow |
| IL3054 | 4 | generic recursion aborted in the message filter tables, which throws if reached |

They come from the security stack (`SctClaimSerializer`, `SignedXMLInternal`, Negotiate), the channel
proxy, and the dispatcher. So: a CoreWCF service *runs* under AOT for this shape, and it does so
without the guarantees a warning-free publish would give. Serialization is one of those warnings
addressed; the rest are not.
