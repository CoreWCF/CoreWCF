Invoke-Expression -Command $PSScriptRoot/Prepare-Run.ps1
try {
  # Run unit tests
  dotnet test $PSScriptRoot/tests/CoreWCF.Templates.Tests.csproj
  Exit $LastExitCode
}
finally {
  Invoke-Expression -Command $PSScriptRoot/Clean-Run.ps1
}

