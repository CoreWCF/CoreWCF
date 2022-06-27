# .NET 6 CoreWCF Sample

This sample shows how to use CoreWCF with .NET 6 using the [Minimal API Syntax](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-6.0). 

This sample uses the BasicHttpBinding and exposes EndPoints at `http://localhost:5000/EchoService/basichttp` and `https://localhost:5001/EchoService/basichttp`. The base url comes from the Urls property in appsettings.json, and the path from the EndPoint registration in code.

## Bindings

The bindings can be changed via code, for example to use WSHttpBinding, the Endpoint can be changed to (or additionally exposed with):

``` C#
    .AddServiceEndpoint<EchoService, IEchoService>(new WSHttpBinding(SecurityMode.Transport), "/EchoService/wshttp");
```

## WSDL

The sample will expose a WSDL endpoint at /EchoService for HTTP & HTTPS.

## Nuget References

The project is configured to pull the CoreWCF binaries from Nuget.org. If you are building CoreWCF locally, change the project references to relative paths:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\CoreWCF.Http\src\CoreWCF.Http.csproj" />
    <ProjectReference Include="..\..\CoreWCF.Primitives\src\CoreWCF.Primitives.csproj" />
  </ItemGroup>
```
