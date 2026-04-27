$ErrorActionPreference = 'Stop'

function Invoke-Checked {
  param([Parameter(Mandatory)][scriptblock] $Action, [string] $Message)
  & $Action
  if ($LASTEXITCODE -ne 0) {
    throw "$Message (exit code $LASTEXITCODE)"
  }
}

# Create required files & folders hierarchy
$SourceDir = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
$TestTemplatesPath = [IO.Path]::Combine($SourceDir, 'TestTemplates')
if (Test-Path $TestTemplatesPath) {
  Remove-Item -Path $TestTemplatesPath -Recurse -Force -ErrorAction Ignore
}
New-Item -Path $TestTemplatesPath -ItemType "directory" -Force
$ArtifactsPath = [IO.Path]::Combine($TestTemplatesPath, 'Artifacts')
New-Item -Path $ArtifactsPath -ItemType "directory" -Force
$DirectoryBuildPropsContent = '<Project></Project>'
$DirectoryBuildPropsContent | Out-File $TestTemplatesPath/Directory.Build.props
$DirectoryBuildTargetsContent = '<Project></Project>'
$DirectoryBuildTargetsContent | Out-File $TestTemplatesPath/Directory.Build.targets
$DirectoryPackagesPropsContent = '<Project></Project>'
$DirectoryPackagesPropsContent | Out-File $TestTemplatesPath/Directory.Packages.props
$NuGetConfigPath = [IO.Path]::Combine($SourceDir, 'NuGet.config')
Copy-Item $NuGetConfigPath -Destination $TestTemplatesPath

# Drop sentinel MSBuild files alongside the in-repo template csproj so that `dotnet add package` and
# `dotnet pack` operate on the template as if it were an independent project, isolated from the repo's
# Directory.Build.props/targets and (most importantly) Directory.Packages.props. Without this, Central
# Package Management inherited from the repo root rejects `dotnet add package -v <version>` and the
# template ships referencing `1.*` from nuget.org instead of the locally-built pre-release packages.
$TemplatesRoot = [IO.Path]::Combine($PSScriptRoot, 'src', 'templates')
$TemplateSentinels = @(
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Build.props'),
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Build.targets'),
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Packages.props')
)
foreach ($sentinel in $TemplateSentinels) {
  '<Project></Project>' | Out-File -FilePath $sentinel -Encoding utf8
}

# Add a local nuget feed to publish current code packages. Remove first in case a prior interrupted
# run left it registered (the script now fails fast on errors, so we make this step idempotent).
$NugetFeedName = 'CoreWCFTemplates-Feed'
dotnet nuget remove source $NugetFeedName 2>$null | Out-Null
$global:LASTEXITCODE = 0
Invoke-Checked { dotnet nuget add source $ArtifactsPath -n $NugetFeedName } 'Failed to add CoreWCFTemplates-Feed nuget source'
# Store current code version in $version
Invoke-Checked { dotnet tool install --tool-path . nbgv } 'Failed to install nbgv tool'
$version = ./nbgv get-version -v NugetPackageVersion
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
  throw "nbgv failed to produce a NugetPackageVersion (exit code $LASTEXITCODE)"
}
# package current runtime code in local nuget feed
Invoke-Checked { dotnet pack $SourceDir/src/CoreWCF.Primitives/src/CoreWCF.Primitives.csproj -o $ArtifactsPath /p:IncludeSymbols=false } 'Failed to pack CoreWCF.Primitives'
Invoke-Checked { dotnet pack $SourceDir/src/CoreWCF.Http/src/CoreWCF.Http.csproj -o $ArtifactsPath /p:IncludeSymbols=false } 'Failed to pack CoreWCF.Http'
# install runtime assemblies in TemplatedProject. The CoreWCFTemplates-Feed source added above is
# globally registered in the user's NuGet config, so 'dotnet add package' can resolve the locally
# packed pre-release versions while still pulling transitive dependencies from upstream feeds.
$TemplatedProjectPath = [IO.Path]::Combine($PSScriptRoot, 'src', 'templates', 'CoreWCFService')
Set-Location $TemplatedProjectPath
Invoke-Checked { dotnet add package CoreWCF.Primitives -v $version } 'Failed to add CoreWCF.Primitives package reference to template'
Invoke-Checked { dotnet add package CoreWCF.Http -v $version } 'Failed to add CoreWCF.Http package reference to template'
Set-Location $PSScriptRoot
# Pack current code (templates) into the nuget feed
Invoke-Checked { dotnet build $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj } 'Failed to build CoreWCF.Templates'
Invoke-Checked { dotnet pack $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj -o $ArtifactsPath /p:IncludeSymbols=false } 'Failed to pack CoreWCF.Templates'
# Install CoreWCF.Templates locally
Invoke-Checked { dotnet new install CoreWCF.Templates::$version --nuget-source $ArtifactsPath } 'Failed to install CoreWCF.Templates'
