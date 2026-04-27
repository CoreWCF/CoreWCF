# Remove TestTemplates folder
$SourceDir = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
$TestTemplatesPath = [IO.Path]::Combine($SourceDir, 'TestTemplates')
if (Test-Path $TestTemplatesPath) {
  Remove-Item -Path $TestTemplatesPath -Recurse -Force -ErrorAction Ignore 
}
# Remove the sentinel MSBuild files dropped by Prepare-Run.ps1 to isolate the template source from the
# repo-root Directory.Build.props/targets/Packages.props during packing.
$TemplatesRoot = [IO.Path]::Combine($PSScriptRoot, 'src', 'templates')
$TemplateSentinels = @(
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Build.props'),
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Build.targets'),
  [IO.Path]::Combine($TemplatesRoot, 'Directory.Packages.props')
)
foreach ($sentinel in $TemplateSentinels) {
  if (Test-Path $sentinel) {
    Remove-Item -Path $sentinel -Force -ErrorAction Ignore
  }
}
# Uninstall CoreWCF.Templates
dotnet new uninstall CoreWCF.Templates
# Delete the temporary feed added above
dotnet nuget remove source CoreWCFTemplates-Feed