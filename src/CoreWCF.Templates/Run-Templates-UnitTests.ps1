try {
  Invoke-Expression -Command $PSScriptRoot/Prepare-Run.ps1
  # Run unit tests
  dotnet test $PSScriptRoot/tests/CoreWCF.Templates.Tests.csproj
  Exit $LastExitCode
}
finally {
  Invoke-Expression -Command $PSScriptRoot/Clean-Run.ps1
}

