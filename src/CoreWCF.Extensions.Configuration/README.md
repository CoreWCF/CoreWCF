# CoreWCF.Extensions.Configuration

Declares CoreWCF bindings, services and endpoints in `IConfiguration`, rather than in the
`<system.serviceModel>` XML that `CoreWCF.ConfigurationManager` reads from a `wcf.config` file.

```jsonc
"ServiceModel": {
  "Bindings": {
    "internal": {
      "Type": "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
      "MaxReceivedMessageSize": 2097152,
      "Security": { "Mode": "Transport" }
    }
  },
  "Services": {
    "Contoso.EchoService, Contoso.Services": {
      "Endpoints": [
        {
          "Contract": "Contoso.IEchoService, Contoso.Contracts",
          "Binding": "internal",
          "Address": "net.tcp://localhost:8089/echo"
        }
      ]
    }
  }
}
```

```csharp
services.AddServiceModelServices();
services.AddServiceModelConfiguration(configuration.GetSection("ServiceModel"));
```

An endpoint's `Binding` is either the name of an entry under `Bindings`, or an inline binding object when the
binding is used once.

## Naming a type

**Every type named in configuration is named by its assembly qualified name** — binding discriminators,
service names and contract names alike. Short names such as `"NetTcpBinding"` are rejected.

This is deliberate, and the reason is in this repository rather than in theory. CoreWCF ships client and server
halves of the queue transports side by side, and they are deliberate homonyms:

| | Type | Assembly |
|---|---|---|
| server | `CoreWCF.Channels.KafkaBinding` | `CoreWCF.Kafka` |
| client | `CoreWCF.ServiceModel.Channels.KafkaBinding` | `CoreWCF.Kafka.Client` |

`RabbitMqBinding` and `RabbitMqTransportBindingElement` are the same story. A registry keyed by short name resolves
`"KafkaBinding"` to whichever assembly it happened to scan last — a silent, ordering-dependent wrong answer. The
namespaces already tell the two apart, because client types live under `CoreWCF.ServiceModel.*` mirroring
`System.ServiceModel.*`, so the full name is the smallest key that cannot collide. Across the 35 binding types in
this repository no full name is duplicated, while three short names are.

Naming the assembly as well is what makes resolution *deterministic*. Transports load lazily, so resolving a name
by searching the assemblies already loaded gives an answer that depends on what the application happened to touch
first — it works on the machine where it was written and fails elsewhere. An assembly qualified name loads its
assembly rather than waiting for something else to.

The same rule covers services and contracts, not only bindings, so a configuration file has one convention rather
than one per kind of type. The determinism argument holds there too: a class library holding service
implementations loads as lazily as a transport does.

This also keeps the package honest about its dependencies: it references `CoreWCF.Primitives` and no transport.
Eleven CoreWCF assemblies declare binding types, from HTTP and net.tcp through to MSMQ, Kafka and RabbitMQ, and
referencing them to populate a registry would drag every transport into an application that wanted one. The
application brings the transports it uses; configuration names them.

The cost is verbosity, and a host that would rather not repeat an assembly qualified name can register its own:

```csharp
services.AddSingleton(new ServiceModelTypeRegistry()
    .Add("netTcp", typeof(CoreWCF.NetTcpBinding))
    .Add("echoService", typeof(Contoso.EchoService))
    .Add("echoContract", typeof(Contoso.IEchoService)));

services.AddServiceModelConfiguration(configuration.GetSection("ServiceModel"));
```

Registered before `AddServiceModelConfiguration`, that registry backs binding discriminators, service names and
contract names alike. Registering two types under one name is an error rather than a silent overwrite.

## Custom bindings

`CustomBinding` takes an ordered list of binding elements, each naming its own type:

```jsonc
"soap12": {
  "Type": "CoreWCF.Channels.CustomBinding, CoreWCF.Primitives",
  "Elements": [
    {
      "Type": "CoreWCF.Channels.TextMessageEncodingBindingElement, CoreWCF.Primitives",
      "MessageVersion": "Soap12WSAddressing10",
      "WriteEncoding": "utf-8"
    },
    {
      "Type": "CoreWCF.Channels.HttpTransportBindingElement, CoreWCF.Http",
      "MaxReceivedMessageSize": 1048576
    }
  ]
}
```

Values such as `MessageVersion`, `EnvelopeVersion`, `SecurityAlgorithmSuite` and `MessageSecurityVersion` have no
`TypeConverter`, so they are resolved by looking up a public static member of the target type by name —
`MessageVersion.Soap12WSAddressing10` here. One lookup covers the whole family and keeps working for types added
later.

## Unknown keys are errors

A key that does not match a property fails, naming its configuration path, rather than being silently ignored.

## Endpoint addresses are a `Uri`

`Address` and `ListenUri` are URIs. An endpoint identity and default addressing headers cannot be declared, and
that is a limitation of CoreWCF's options API rather than of this package: `ServiceConfigurationBuilder` exposes
only `AddServiceEndpoint(Type, Binding, Uri, Uri)`, so there is nowhere to hand a full `EndpointAddress` even
though `ServiceEndpoint.Address` is settable and `IServiceBuilder` has the `Action<ServiceEndpoint>` overload that
would carry it. Tracked as [CoreWCF#1763](https://github.com/CoreWCF/CoreWCF/issues/1763).

Address headers would remain out of scope regardless: an `AddressHeader`'s value is arbitrary
DataContract-serialised XML, which a key/value configuration source cannot represent faithfully.
