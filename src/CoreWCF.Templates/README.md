# CoreWCF.Templates

## Running unit tests CI

1. Run the script [Run-Templates-UnitTests.ps1](./Run-Templates-UnitTests.ps1).
```powershell
./Run-Templates-UnitTests.ps1
```

## Running unit tests locally

1. Run the script [Prepare-Run.ps1](./Prepare-Run.ps1)
```powershell
./Prepare-Run.ps1
```

2. Run unit tests. Use your favorite IDE or run
```powershell
dotnet test ./tests/CoreWCF.Templates.Tests.csproj
```

3. Run the script [Clean-Run.ps1](./Clean-Run.ps1)
```powershell
./Clean-Run.ps1
```
