# Remove TestTemplates folder
$SourceDir = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
$TestTemplatesPath = [IO.Path]::Combine($SourceDir, 'TestTemplates')
if (Test-Path $TestTemplatesPath) {
  Remove-Item -Path $TestTemplatesPath -Recurse -Force -ErrorAction Ignore 
}
# Uninstall CoreWCF.Templates
dotnet new uninstall CoreWCF.Templates
# Delete the temporary feed added above
dotnet nuget remove source CoreWCFTemplates-Feed