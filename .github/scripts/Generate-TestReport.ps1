# Generate a self-contained, filterable HTML report from a tree of TRX files.
#
# Layout expected under -TrxRoot (matches actions/download-artifact@v8 with
# pattern 'test-results-pr-*' and merge-multiple unset):
#
#   <TrxRoot>/test-results-pr-<Config>-<OS>-<TFM>/.../*.trx
#
# The artifact folder name is parsed to recover (Configuration, OS, TFM). Each
# UnitTestResult becomes one row; failed/skipped tests carry their error message
# and stack trace, passed tests carry only minimal metadata to keep the page
# small. The output is a single index.html plus summary.json (consumed by the
# workflow to build the sticky PR comment).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $TrxRoot,
    [Parameter(Mandatory = $true)] [string] $OutputDir,
    [Parameter(Mandatory = $false)] [string] $Title = 'CoreWCF Test Results',
    [Parameter(Mandatory = $false)] [string] $Subtitle = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$trxNs = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

function Get-MatrixFromArtifactName {
    param([string] $Name)

    if ($Name -match '^test-results-pr-(?<cfg>Debug|Release)-(?<os>Linux|Windows|macOS)-(?<tfm>[A-Za-z0-9.+-]+)$') {
        return @{
            Configuration   = $Matches['cfg']
            OperatingSystem = $Matches['os']
            TargetFramework = $Matches['tfm']
        }
    }

    return @{
        Configuration   = 'unknown'
        OperatingSystem = 'unknown'
        TargetFramework = 'unknown'
    }
}

function Get-NormalizedOutcome {
    param([string] $Outcome)

    switch ($Outcome) {
        'Passed'       { 'passed';  break }
        'Failed'       { 'failed';  break }
        'NotExecuted'  { 'skipped'; break }
        'Inconclusive' { 'skipped'; break }
        'NotRunnable'  { 'skipped'; break }
        'Pending'      { 'skipped'; break }
        default        { 'other' }
    }
}

function Encode-Html {
    param([string] $Text)
    if ($null -eq $Text) { return '' }
    return ($Text -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' -replace '"','&quot;')
}

if (-not (Test-Path -LiteralPath $TrxRoot)) {
    Write-Host "::warning::TRX root '$TrxRoot' does not exist; emitting empty report."
    $trxFiles = @()
} else {
    $trxFiles = @(Get-ChildItem -LiteralPath $TrxRoot -Filter *.trx -Recurse -File)
}
Write-Host "Found $($trxFiles.Count) TRX file(s) under '$TrxRoot'."

$results = New-Object System.Collections.Generic.List[object]
$rootFull = if (Test-Path -LiteralPath $TrxRoot) { (Resolve-Path -LiteralPath $TrxRoot).Path } else { $TrxRoot }

foreach ($trx in $trxFiles) {
    $rel = $trx.FullName.Substring($rootFull.Length).TrimStart([char]'\', [char]'/')
    $first = ($rel -split '[\\/]', 2)[0]
    $matrix = Get-MatrixFromArtifactName $first

    try {
        [xml]$doc = Get-Content -LiteralPath $trx.FullName -Raw
    } catch {
        Write-Host "::warning::Failed to parse '$($trx.FullName)': $_"
        continue
    }

    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('t', $trxNs)

    # Build a testId -> (className, assembly) lookup once per file.
    $defs = @{}
    foreach ($d in $doc.SelectNodes('//t:UnitTest', $ns)) {
        $tm = $d.SelectSingleNode('t:TestMethod', $ns)
        $className = ''
        $assembly = ''
        if ($tm) {
            if ($tm.HasAttribute('className')) { $className = $tm.GetAttribute('className') }
            if ($tm.HasAttribute('codeBase')) {
                $assembly = [System.IO.Path]::GetFileNameWithoutExtension($tm.GetAttribute('codeBase'))
            }
        }
        $defs[$d.GetAttribute('id')] = [pscustomobject]@{
            ClassName = $className
            Assembly  = $assembly
        }
    }

    foreach ($r in $doc.SelectNodes('//t:UnitTestResult', $ns)) {
        $rawOutcome = $r.GetAttribute('outcome')
        $outcome = Get-NormalizedOutcome $rawOutcome
        $testId = $r.GetAttribute('testId')
        $info = if ($defs.ContainsKey($testId)) { $defs[$testId] } else { $null }

        $message = $null
        $stack = $null
        if ($outcome -ne 'passed') {
            $errNode = $r.SelectSingleNode('t:Output/t:ErrorInfo', $ns)
            if ($errNode) {
                $msgNode = $errNode.SelectSingleNode('t:Message', $ns)
                if ($msgNode) { $message = $msgNode.InnerText }
                $stackNode = $errNode.SelectSingleNode('t:StackTrace', $ns)
                if ($stackNode) { $stack = $stackNode.InnerText }
            }
        }

        $results.Add([pscustomobject]@{
            n   = $r.GetAttribute('testName')
            c   = if ($info) { $info.ClassName } else { '' }
            a   = if ($info) { $info.Assembly  } else { '' }
            o   = $outcome
            d   = $r.GetAttribute('duration')
            cfg = $matrix.Configuration
            os  = $matrix.OperatingSystem
            tfm = $matrix.TargetFramework
            m   = $message
            s   = $stack
        })
    }
}

$total = $results.Count
$passed  = @($results | Where-Object { $_.o -eq 'passed'  }).Count
$failed  = @($results | Where-Object { $_.o -eq 'failed'  }).Count
$skipped = @($results | Where-Object { $_.o -eq 'skipped' }).Count
$other   = $total - $passed - $failed - $skipped

# Per-matrix-cell breakdown for the dashboard grid.
$matrixCells = @()
foreach ($g in ($results | Group-Object -Property { "$($_.cfg)|$($_.os)|$($_.tfm)" })) {
    $parts = $g.Name -split '\|'
    $matrixCells += [pscustomobject]@{
        cfg     = $parts[0]
        os      = $parts[1]
        tfm     = $parts[2]
        total   = $g.Count
        passed  = @($g.Group | Where-Object { $_.o -eq 'passed'  }).Count
        failed  = @($g.Group | Where-Object { $_.o -eq 'failed'  }).Count
        skipped = @($g.Group | Where-Object { $_.o -eq 'skipped' }).Count
    }
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

# summary.json is consumed by the workflow's PR-comment step; keep the schema
# stable.
$summary = [pscustomobject]@{
    total    = $total
    passed   = $passed
    failed   = $failed
    skipped  = $skipped
    other    = $other
    matrix   = $matrixCells
    title    = $Title
    subtitle = $Subtitle
}
$summary | ConvertTo-Json -Depth 6 | Out-File -LiteralPath (Join-Path $OutputDir 'summary.json') -Encoding utf8

# Strip empty fields from the wire data to keep the payload small. Passed tests
# never carry message/stack; treat empty strings the same way.
$wireRows = foreach ($r in $results) {
    $row = [ordered]@{
        n   = $r.n
        c   = $r.c
        a   = $r.a
        o   = $r.o
        cfg = $r.cfg
        os  = $r.os
        tfm = $r.tfm
    }
    if ($r.d) { $row.d = $r.d }
    if ($r.m) { $row.m = $r.m }
    if ($r.s) { $row.s = $r.s }
    [pscustomobject]$row
}

$dataJson = ($wireRows | ConvertTo-Json -Depth 4 -Compress)
if (-not $dataJson) { $dataJson = '[]' }
# ConvertTo-Json with a single object returns an object literal, not an array;
# normalise so the JS always sees an array.
if ($dataJson.StartsWith('{')) { $dataJson = "[$dataJson]" }

$summaryJsonInline = ($summary | ConvertTo-Json -Depth 6 -Compress)

$titleEsc    = Encode-Html $Title
$subtitleEsc = Encode-Html $Subtitle

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>$titleEsc</title>
<style>
  :root {
    --bg: #0d1117; --fg: #e6edf3; --muted: #8b949e; --panel: #161b22;
    --border: #30363d; --accent: #58a6ff; --pass: #3fb950; --fail: #f85149;
    --skip: #d29922; --code-bg: #1c2128;
  }
  * { box-sizing: border-box; }
  body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI",
         system-ui, sans-serif; background: var(--bg); color: var(--fg);
         line-height: 1.45; }
  header { padding: 24px; border-bottom: 1px solid var(--border); }
  h1 { margin: 0 0 4px 0; font-size: 22px; }
  .subtitle { color: var(--muted); font-size: 13px; }
  main { padding: 24px; max-width: 1400px; margin: 0 auto; }
  .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
           gap: 12px; margin-bottom: 24px; }
  .card { background: var(--panel); border: 1px solid var(--border); border-radius: 8px;
          padding: 14px 16px; }
  .card h2 { margin: 0 0 4px 0; font-size: 11px; text-transform: uppercase;
             letter-spacing: 0.05em; color: var(--muted); font-weight: 600; }
  .card .v { font-size: 26px; font-weight: 600; }
  .card.pass .v { color: var(--pass); }
  .card.fail .v { color: var(--fail); }
  .card.skip .v { color: var(--skip); }
  .matrix { background: var(--panel); border: 1px solid var(--border);
            border-radius: 8px; padding: 12px; margin-bottom: 24px;
            overflow-x: auto; }
  .matrix table { border-collapse: collapse; width: 100%; min-width: 600px;
                  font-size: 13px; }
  .matrix th, .matrix td { padding: 6px 10px; text-align: left;
                            border-bottom: 1px solid var(--border); }
  .matrix th { color: var(--muted); font-weight: 600; font-size: 11px;
               text-transform: uppercase; letter-spacing: 0.05em; }
  .matrix td.num { text-align: right; font-variant-numeric: tabular-nums; }
  .matrix .ok { color: var(--pass); }
  .matrix .bad { color: var(--fail); font-weight: 600; }
  .matrix .warn { color: var(--skip); }
  .filters { display: flex; flex-wrap: wrap; gap: 8px; align-items: center;
             margin-bottom: 12px; }
  .filters select, .filters input { background: var(--panel); color: var(--fg);
             border: 1px solid var(--border); border-radius: 6px;
             padding: 6px 10px; font-size: 13px; }
  .filters input[type="search"] { flex: 1 1 240px; min-width: 220px; }
  .filters .count { color: var(--muted); font-size: 12px; margin-left: auto; }
  .results { background: var(--panel); border: 1px solid var(--border);
             border-radius: 8px; }
  .row { border-bottom: 1px solid var(--border); }
  .row:last-child { border-bottom: 0; }
  .row .head { display: grid; grid-template-columns: 80px 1fr auto;
               align-items: center; gap: 12px; padding: 10px 14px;
               cursor: pointer; user-select: none; }
  .row .head:hover { background: rgba(110, 118, 129, 0.1); }
  .badge { display: inline-block; padding: 2px 8px; border-radius: 999px;
           font-size: 11px; font-weight: 600; text-align: center; }
  .badge.passed  { background: rgba(63, 185, 80, 0.15);  color: var(--pass); }
  .badge.failed  { background: rgba(248, 81, 73, 0.15);  color: var(--fail); }
  .badge.skipped { background: rgba(210, 153, 34, 0.15); color: var(--skip); }
  .badge.other   { background: rgba(139, 148, 158, 0.15); color: var(--muted); }
  .name { font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, monospace;
          font-size: 13px; word-break: break-all; }
  .meta { color: var(--muted); font-size: 11px; white-space: nowrap; }
  .detail { display: none; padding: 10px 14px 14px 14px; background: var(--code-bg);
            border-top: 1px solid var(--border); }
  .row.expanded .detail { display: block; }
  .detail h4 { margin: 8px 0 4px 0; font-size: 11px; text-transform: uppercase;
               letter-spacing: 0.05em; color: var(--muted); font-weight: 600; }
  .detail pre { margin: 0; padding: 10px; background: var(--bg);
                border: 1px solid var(--border); border-radius: 6px;
                font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, monospace;
                font-size: 12px; overflow-x: auto; white-space: pre-wrap;
                word-break: break-word; }
  .pager { padding: 12px; text-align: center; color: var(--muted); font-size: 12px; }
  .pager button { background: var(--panel); color: var(--fg);
                  border: 1px solid var(--border); border-radius: 6px;
                  padding: 6px 14px; font-size: 13px; cursor: pointer;
                  margin: 0 4px; }
  .pager button:hover:not(:disabled) { border-color: var(--accent); }
  .pager button:disabled { opacity: 0.5; cursor: not-allowed; }
  .empty { padding: 32px; text-align: center; color: var(--muted); }
</style>
</head>
<body>
<header>
  <h1>$titleEsc</h1>
  <div class="subtitle">$subtitleEsc</div>
</header>
<main>
  <div class="cards">
    <div class="card"><h2>Total</h2><div class="v" id="card-total">0</div></div>
    <div class="card pass"><h2>Passed</h2><div class="v" id="card-passed">0</div></div>
    <div class="card fail"><h2>Failed</h2><div class="v" id="card-failed">0</div></div>
    <div class="card skip"><h2>Skipped</h2><div class="v" id="card-skipped">0</div></div>
  </div>

  <div class="matrix" id="matrix"></div>

  <div class="filters">
    <select id="f-outcome">
      <option value="failed">Failed only</option>
      <option value="all">All outcomes</option>
      <option value="passed">Passed only</option>
      <option value="skipped">Skipped only</option>
    </select>
    <select id="f-cfg"><option value="">All configs</option></select>
    <select id="f-os"><option value="">All OS</option></select>
    <select id="f-tfm"><option value="">All TFMs</option></select>
    <input type="search" id="f-text" placeholder="Filter test name, class, error message..." />
    <span class="count" id="count"></span>
  </div>

  <div class="results" id="results"></div>
  <div class="pager" id="pager"></div>
</main>

<script>
  const DATA = $dataJson;
  const SUMMARY = $summaryJsonInline;
  const PAGE_SIZE = 200;
  let page = 0;

  function el(tag, attrs, children) {
    const e = document.createElement(tag);
    if (attrs) for (const k in attrs) {
      if (k === 'class') e.className = attrs[k];
      else if (k === 'text') e.textContent = attrs[k];
      else e.setAttribute(k, attrs[k]);
    }
    if (children) for (const c of children) {
      if (c) e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
    }
    return e;
  }

  function uniqSorted(arr) {
    return Array.from(new Set(arr)).filter(x => x).sort();
  }

  function populateSelect(id, values) {
    const sel = document.getElementById(id);
    for (const v of values) sel.appendChild(el('option', { value: v, text: v }));
  }

  function renderCards() {
    document.getElementById('card-total').textContent   = SUMMARY.total.toLocaleString();
    document.getElementById('card-passed').textContent  = SUMMARY.passed.toLocaleString();
    document.getElementById('card-failed').textContent  = SUMMARY.failed.toLocaleString();
    document.getElementById('card-skipped').textContent = SUMMARY.skipped.toLocaleString();
  }

  function renderMatrix() {
    const cells = SUMMARY.matrix || [];
    const host = document.getElementById('matrix');
    if (cells.length === 0) {
      host.innerHTML = '<div class="empty">No test runs detected.</div>';
      return;
    }
    cells.sort((a, b) => (a.cfg + a.os + a.tfm).localeCompare(b.cfg + b.os + b.tfm));
    const tbl = el('table');
    const thead = el('thead');
    const trh = el('tr');
    for (const h of ['Configuration','OS','TFM','Total','Passed','Failed','Skipped']) {
      trh.appendChild(el('th', null, [h]));
    }
    thead.appendChild(trh);
    tbl.appendChild(thead);
    const tbody = el('tbody');
    for (const c of cells) {
      const tr = el('tr');
      tr.appendChild(el('td', null, [c.cfg]));
      tr.appendChild(el('td', null, [c.os]));
      tr.appendChild(el('td', null, [c.tfm]));
      tr.appendChild(el('td', { class: 'num' }, [String(c.total)]));
      tr.appendChild(el('td', { class: 'num ok' }, [String(c.passed)]));
      tr.appendChild(el('td', { class: 'num ' + (c.failed > 0 ? 'bad' : '') }, [String(c.failed)]));
      tr.appendChild(el('td', { class: 'num ' + (c.skipped > 0 ? 'warn' : '') }, [String(c.skipped)]));
      tbody.appendChild(tr);
    }
    tbl.appendChild(tbody);
    host.appendChild(tbl);
  }

  function getFilters() {
    return {
      outcome: document.getElementById('f-outcome').value,
      cfg: document.getElementById('f-cfg').value,
      os:  document.getElementById('f-os').value,
      tfm: document.getElementById('f-tfm').value,
      text: document.getElementById('f-text').value.trim().toLowerCase()
    };
  }

  function applyFilters() {
    const f = getFilters();
    return DATA.filter(r => {
      if (f.outcome !== 'all' && r.o !== f.outcome) return false;
      if (f.cfg && r.cfg !== f.cfg) return false;
      if (f.os  && r.os  !== f.os) return false;
      if (f.tfm && r.tfm !== f.tfm) return false;
      if (f.text) {
        const hay = ((r.n || '') + ' ' + (r.c || '') + ' ' + (r.m || '')).toLowerCase();
        if (!hay.includes(f.text)) return false;
      }
      return true;
    });
  }

  function renderResults() {
    const filtered = applyFilters();
    const host = document.getElementById('results');
    host.innerHTML = '';
    document.getElementById('count').textContent =
      filtered.length.toLocaleString() + ' of ' + DATA.length.toLocaleString() + ' result(s)';

    if (filtered.length === 0) {
      host.appendChild(el('div', { class: 'empty' }, ['No results match the current filters.']));
      document.getElementById('pager').innerHTML = '';
      return;
    }

    const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
    if (page >= totalPages) page = 0;
    const slice = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);

    for (const r of slice) {
      const row = el('div', { class: 'row' });
      const head = el('div', { class: 'head' });
      head.appendChild(el('span', { class: 'badge ' + r.o }, [r.o]));
      const name = el('div', { class: 'name' });
      name.appendChild(document.createTextNode(r.n));
      if (r.c) {
        const sub = el('div', { class: 'meta' });
        sub.textContent = r.c;
        name.appendChild(sub);
      }
      head.appendChild(name);
      const meta = el('div', { class: 'meta' });
      meta.textContent = r.cfg + ' \u00b7 ' + r.os + ' \u00b7 ' + r.tfm;
      head.appendChild(meta);
      row.appendChild(head);

      if (r.m || r.s) {
        const detail = el('div', { class: 'detail' });
        if (r.m) {
          detail.appendChild(el('h4', null, ['Message']));
          detail.appendChild(el('pre', { text: r.m }));
        }
        if (r.s) {
          detail.appendChild(el('h4', null, ['Stack trace']));
          detail.appendChild(el('pre', { text: r.s }));
        }
        row.appendChild(detail);
        head.addEventListener('click', () => row.classList.toggle('expanded'));
        if (r.o === 'failed') row.classList.add('expanded');
      }
      host.appendChild(row);
    }

    const pager = document.getElementById('pager');
    pager.innerHTML = '';
    if (totalPages > 1) {
      const prev = el('button', null, ['\u25c0 Prev']);
      prev.disabled = page === 0;
      prev.onclick = () => { page--; renderResults(); window.scrollTo({ top: host.offsetTop - 20 }); };
      const next = el('button', null, ['Next \u25b6']);
      next.disabled = page >= totalPages - 1;
      next.onclick = () => { page++; renderResults(); window.scrollTo({ top: host.offsetTop - 20 }); };
      pager.appendChild(prev);
      pager.appendChild(document.createTextNode(' Page ' + (page + 1) + ' of ' + totalPages + ' '));
      pager.appendChild(next);
    }
  }

  populateSelect('f-cfg', uniqSorted(DATA.map(r => r.cfg)));
  populateSelect('f-os',  uniqSorted(DATA.map(r => r.os)));
  populateSelect('f-tfm', uniqSorted(DATA.map(r => r.tfm)));

  for (const id of ['f-outcome','f-cfg','f-os','f-tfm']) {
    document.getElementById(id).addEventListener('change', () => { page = 0; renderResults(); });
  }
  let textTimer;
  document.getElementById('f-text').addEventListener('input', () => {
    clearTimeout(textTimer);
    textTimer = setTimeout(() => { page = 0; renderResults(); }, 150);
  });

  renderCards();
  renderMatrix();
  renderResults();
</script>
</body>
</html>
"@

$html | Out-File -LiteralPath (Join-Path $OutputDir 'index.html') -Encoding utf8

Write-Host ("Wrote test report: total={0} passed={1} failed={2} skipped={3} other={4}" -f $total, $passed, $failed, $skipped, $other)
