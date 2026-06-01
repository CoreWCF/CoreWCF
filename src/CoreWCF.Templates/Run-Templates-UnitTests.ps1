[CmdletBinding()]
param(
  # Optional `dotnet test --filter` expression. Used by CI to fan out the Windows job into a
  # .NET Core slice and a .NET Framework slice that run in parallel.
  [string] $Filter
)

try {
  Invoke-Expression -Command $PSScriptRoot/Prepare-Run.ps1
  # Run unit tests
  $testArgs = @($PSScriptRoot + '/tests/CoreWCF.Templates.Tests.csproj')
  if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $testArgs += @('--filter', $Filter)
  }
  dotnet test @testArgs
  Exit $LastExitCode
}
finally {
  Invoke-Expression -Command $PSScriptRoot/Clean-Run.ps1
}
