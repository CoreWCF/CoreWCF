<#
.SYNOPSIS
    Imports or re-syncs contract type files from dotnet/runtime's serialization test suite.

.DESCRIPTION
    Downloads each file at a pinned commit, checks the licence header, injects a provenance block
    and writes it next to this script with CRLF line endings and no byte-order mark.

    Always pin a commit SHA, never a branch: an upstream edit to a populating constructor silently
    changes what the golden fixtures record, and a floating reference would make that change
    invisible in review.

    On re-sync the script reports any local "// CoreWCF:" modifications in the file it is about to
    overwrite. It cannot re-apply them - they must be restored by hand from the backup it leaves.

.PARAMETER Sha
    Upstream commit to pin to.

.PARAMETER File
    One or more file names, e.g. InheritanceCases.cs.

.PARAMETER WhatIf
    Report what would change without writing anything.

.EXAMPLE
    ./import.ps1 -Sha bbfaee3bfa7edb0d556556bc32778d09a745134b -File Primitives.cs,InheritanceCases.cs

.OUTPUTS
    A row per file, ready to paste into the Files table in UPSTREAM.md.

.NOTES
    After importing: build every target framework, resolve breaks with a "// CoreWCF:" marker,
    register or skip every new [DataContract] type in a Catalog/CorpusCatalog.*.cs registrar, then
    regenerate fixtures and read the diff.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $Sha,

    [Parameter(Mandatory = $true)]
    [string[]] $File,

    [string] $Repo = 'dotnet/runtime',

    [string] $UpstreamPath = 'src/libraries/System.Runtime.Serialization.Xml/tests/SerializationTestTypes'
)

$ErrorActionPreference = 'Stop'

$licenceHeader = @(
    '// Licensed to the .NET Foundation under one or more agreements.'
    '// The .NET Foundation licenses this file to you under the MIT license.'
)

$destinationDirectory = $PSScriptRoot
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$rows = @()

foreach ($name in $File) {
    $url = "https://raw.githubusercontent.com/$Repo/$Sha/$UpstreamPath/$name"
    $destination = Join-Path $destinationDirectory $name

    Write-Host "==> $name" -ForegroundColor Cyan
    Write-Host "    $url"

    $temporary = [System.IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporary -Headers @{ 'User-Agent' = 'corewcf-corpus-import' } | Out-Null
        $lines = [System.IO.File]::ReadAllLines($temporary)
    }
    finally {
        # -WhatIf:$false so a dry run still cleans up after itself.
        Remove-Item $temporary -ErrorAction SilentlyContinue -WhatIf:$false
    }

    if ($lines.Count -lt 3) {
        throw "$name looks empty or truncated ($($lines.Count) lines)."
    }

    # Guard against a moved path silently yielding a 404 page, and against upstream relicensing.
    for ($i = 0; $i -lt $licenceHeader.Count; $i++) {
        if ($lines[$i].Trim() -ne $licenceHeader[$i]) {
            throw "$name does not start with the expected MIT header. Line $($i + 1) was: '$($lines[$i])'"
        }
    }

    $provenance = @(
        '//'
        "// Copied verbatim from $Repo at $Sha`:"
        "// https://github.com/$Repo/blob/$Sha/$UpstreamPath/$name"
        '// Local modifications are marked with a "// CoreWCF:" comment and listed in UPSTREAM.md.'
    )

    $composed = @($lines[0..1]) + $provenance + @($lines[2..($lines.Count - 1)])
    $newText = ($composed -join "`r`n") + "`r`n"

    $status = 'created'
    if (Test-Path $destination) {
        $existingText = [System.IO.File]::ReadAllText($destination)
        if ($existingText -eq $newText) {
            $status = 'unchanged'
        }
        else {
            $status = 'updated'
        }

        # Anchored so the provenance block, which quotes the marker in prose, is not counted.
        $markers = Select-String -Path $destination -Pattern '^\s*// CoreWCF:'
        if ($markers.Count -gt 0 -and $status -eq 'updated') {
            $backup = "$destination.bak"
            if ($PSCmdlet.ShouldProcess($backup, 'write backup')) {
                Copy-Item $destination $backup -Force
            }

            Write-Warning "$name has $($markers.Count) local modification(s) that this overwrite discards. Re-apply them from $([System.IO.Path]::GetFileName($backup)):"
            foreach ($marker in $markers) {
                Write-Warning "    L$($marker.LineNumber): $($marker.Line.Trim())"
            }
        }
    }

    if ($status -eq 'unchanged') {
        Write-Host "    unchanged" -ForegroundColor DarkGray
    }
    elseif ($PSCmdlet.ShouldProcess($destination, $status)) {
        [System.IO.File]::WriteAllText($destination, $newText, $utf8NoBom)
        Write-Host "    $status ($($composed.Count) lines)" -ForegroundColor Green
    }

    $contractCount = ($composed | Select-String -Pattern '^\s*\[DataContract').Count
    $rows += "| ``$name`` | ``$UpstreamPath/$name`` | Imported in full; $contractCount contract declarations. |"
}

Write-Host ''
Write-Host 'Paste into the Files table in UPSTREAM.md, and update the pinned SHA there:' -ForegroundColor Cyan
Write-Host "  Pinned commit: $Sha"
$rows | ForEach-Object { Write-Host "  $_" }

Write-Host ''
Write-Host 'Next:' -ForegroundColor Cyan
Write-Host '  1. dotnet build src\CoreWCF.DataContractSerialization.TestCorpus\src\CoreWCF.DataContractSerialization.TestCorpus.csproj -c Debug'
Write-Host '     Resolve every break with a "// CoreWCF:" marker and record it in UPSTREAM.md.'
Write-Host '  2. Register or skip each new [DataContract] type in Catalog\CorpusCatalog.<file>.cs.'
Write-Host '     CorpusIntegrityTests fails until every one is accounted for.'
Write-Host '  3. Regenerate fixtures (see ..\..\..\CoreWCF.DataContractSerialization\README.md) and read the diff.'
