# CoreWCF.Templates

This describe how to debug and test the CoreWCF templates on a developer machine. 

If you just want to use the templates, see [Use CoreWCF project templates (dotnet CLI or VisualStudio](./../../README.md#use-core-wcf-project-templates-dotnet-cli-or-visualstudio).

## Test the templates locally

Ensure CoreWCF templates are not already installed on your machine by first running 
```cmd
dotnet new --uninstall CoreWCF.Templates
```

- Run the script [Prepare-Run.ps1](./Prepare-Run.ps1)
```powershell
./Prepare-Run.ps1
```

- Open a command line and  run

```cmd
dotnet new corewcf
```

- Using VS2022 you should find **CoreWCF Service** project template

- Inspect and build the created project

- Clean up your environment. Run the script [Clean-Run.ps1](./Clean-Run.ps1)
```powershell
./Clean-Run.ps1
```

## Running unit tests locally

1. Run the script [Prepare-Run.ps1](./Prepare-Run.ps1)
```powershell
./Prepare-Run.ps1
```

2. Run unit tests from any IDE or run
```powershell
dotnet test ./tests/CoreWCF.Templates.Tests.csproj
```

3. Run the script [Clean-Run.ps1](./Clean-Run.ps1)
```powershell
./Clean-Run.ps1
```

## Running unit tests (CI)

1. Run the script [Run-Templates-UnitTests.ps1](./Run-Templates-UnitTests.ps1).
```powershell
./Run-Templates-UnitTests.ps1
```