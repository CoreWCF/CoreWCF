dotnet.exe build src\CoreWCF.Http\src\CoreWCF.Http.csproj --configuration Release /t:restore
dotnet.exe build src\CoreWCF.NetTcp\src\CoreWCF.NetTcp.csproj --configuration Release /t:restore
dotnet.exe build src\CoreWCF.Primitives\src\CoreWCF.Primitives.csproj --configuration Release /t:restore

dotnet.exe build src\CoreWCF.Http\src\CoreWCF.Http.csproj --no-restore --configuration Release
dotnet.exe build src\CoreWCF.NetTcp\src\CoreWCF.NetTcp.csproj --no-restore --configuration Release
dotnet.exe build src\CoreWCF.Primitives\src\CoreWCF.Primitives.csproj --no-restore --configuration Release

dotnet build src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj --configuration Release /t:restore
dotnet build src/CoreWCF.NetTcp/tests/CoreWCF.NetTcp.Tests.csproj --configuration Release /t:restore
dotnet build src/CoreWCF.Primitives/tests/CoreWCF.Primitives.Tests.csproj --configuration Release /t:restore

dotnet build src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj --configuration Release --framework netcoreapp2.1
dotnet build src/CoreWCF.NetTcp/tests/CoreWCF.NetTcp.Tests.csproj --configuration Release --framework netcoreapp2.1
dotnet build src/CoreWCF.Primitives/tests/CoreWCF.Primitives.Tests.csproj --configuration Release --framework netcoreapp2.1

dotnet test src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj --no-restore --configuration Release --framework netcoreapp2.1
dotnet test src/CoreWCF.NetTcp/tests/CoreWCF.NetTcp.Tests.csproj --no-restore --configuration Release --framework netcoreapp2.1
dotnet test src/CoreWCF.Primitives/tests/CoreWCF.Primitives.Tests --no-restore --configuration Release --framework netcoreapp2.1