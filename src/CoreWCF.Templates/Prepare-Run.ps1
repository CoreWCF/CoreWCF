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
$DirectoryBuildTargetsContent | Out-File $TestTemplatesPath/Directory.Build.target
# Add a local nuget feed to publish current code packages
$NugetFeedName = 'CoreWCFTemplates-Feed'
dotnet nuget add source $ArtifactsPath -n $NugetFeedName
# Store current code version in $version
dotnet tool install --tool-path . nbgv
$version = ./nbgv get-version -v NugetPackageVersion
# install runtime assemblies in TemplatedProject
$TemplatedProjectPath = [IO.Path]::Combine($PSScriptRoot, 'src', 'templates', 'CoreWCFService')
Set-Location $TemplatedProjectPath
dotnet add package CoreWCF.Primitives -v 1.*-*
dotnet add package CoreWCF.Http -v 1.*-*
Set-Location $PSScriptRoot
# Pack current code (templates) into the nuget feed
dotnet build $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj
dotnet pack $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj -o $ArtifactsPath /p:IncludeSymbols=false
# Install CoreWCF.Templates locally
dotnet new --install CoreWCF.Templates::$version --nuget-source $ArtifactsPath
