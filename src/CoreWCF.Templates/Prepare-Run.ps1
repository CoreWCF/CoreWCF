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
# Pack current code into the nuget feed
dotnet build $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj
dotnet pack $SourceDir/src/CoreWCF.Templates/src/CoreWCF.Templates.csproj -o $ArtifactsPath /p:IncludeSymbols=false
# Install CoreWCF.Templates locally
dotnet new --install CoreWCF.Templates