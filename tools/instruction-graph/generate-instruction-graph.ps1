param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$Banner = "Generated diagnostic artifact. Do not hand-edit. Not a source of truth. Root authority remains AGENTS.md."
$OutputDir = Join-Path $PSScriptRoot "out"
$JsonPath = Join-Path $OutputDir "instruction-graph.json"
$HtmlPath = Join-Path $OutputDir "instruction-graph.html"

function ConvertTo-RepoPath {
    param([string]$Path)
    $rootPath = (Resolve-Path $RepoRoot).Path.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri((Resolve-Path $Path).Path)
    $relative = [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
    return ($relative -replace "\\", "/")
}

function Get-Title {
    param([string[]]$Lines, [string]$Fallback)
    foreach ($line in $Lines) {
        if ($line -match '^\s*#\s+(.+?)\s*$') {
            return $Matches[1]
        }
    }
    return $Fallback
}

function Get-NodeType {
    param([string]$Path)
    switch -Regex ($Path) {
        '^AGENTS\.md$' { return "root_authority" }
        '^docs/AGENT_CONTEXT\.md$' { return "context_router" }
        '^\.cursor/rules/' { return "tool_adapter" }
        '^docs/audits/' { return "historical_or_deprecated" }
        '^docs/(PR_POST_OPEN_AGENT_LOOP|WORKTREE_BRANCH_HYGIENE|agent-pr-review-checklist)\.md$' { return "workflow_doc" }
        '^docs/(architecture|TEST_HARNESS|COMBAT_FEEDBACK_POLICY|TARGETING_GLOSSARY|UnityCSharp-CodeOrganization)\.md$' { return "domain_doc" }
        default { return "domain_doc" }
    }
}

function Get-RuleKinds {
    param([string]$Text, [string]$Path)
    $kinds = New-Object System.Collections.Generic.List[string]
    $checks = [ordered]@{
        workflow = '(?i)\bworkflow\b|\bPR\b|pull request|task contract|context bundle|source[- ]of[- ]truth'
        technical = '(?i)FixedUpdateNetwork|Update|LateUpdate|MonoBehaviour|physics|collider|serialized|prefab|scene|NetworkBehaviour'
        git = '(?i)\bgit\b|branch|commit|push|merge|rebase|stage|staging|worktree'
        verification = '(?i)verification|verify|green test|definition of done|manual smoke'
        authority = '(?i)State Authority|authority|client.*intent|damage|combat result'
        testing = '(?i)EditMode|PlayMode|Test Runner|test harness|Unity Test Framework'
        ui = '(?i)\bUI\b|HUD|camera|cursor|targeting|floating combat text|screen-space'
        historical = '(?i)historical|deprecated|removed|superseded|checkpoint|audit'
    }
    foreach ($key in $checks.Keys) {
        if ($Text -match $checks[$key]) {
            $kinds.Add($key)
        }
    }
    if ($Path -match '^\.cursor/rules/') {
        $kinds.Add("workflow")
    }
    return @($kinds | Select-Object -Unique)
}

function Get-Summary {
    param([string]$Path, [string]$Type)
    switch ($Type) {
        "root_authority" { return "Shared root operating guide for agent safety, workflow, and technical constraints." }
        "context_router" { return "Routes tasks to the smallest current context bundle and lists historical docs." }
        "tool_adapter" { return "Tool-specific adapter or focused rule; should mirror or specialize repository guidance." }
        "workflow_doc" { return "Workflow-specific guidance that extends repository operating rules." }
        "historical_or_deprecated" { return "Historical or one-time audit material; diagnostic context, not current authority." }
        default { return "Domain guidance or project reference material." }
    }
}

function Resolve-ReferencePath {
    param([string]$Reference, [string]$FromPath)

    $candidate = ($Reference -replace "\\", "/").Trim()
    $candidate = $candidate.TrimStart("./")

    if ($candidate -match '^(AGENTS\.md|docs/|\.cursor/rules/|Assets/|tools/)') {
        return $candidate
    }

    if ($candidate -match '^[A-Za-z0-9._-]+\.md$') {
        $fromDir = Split-Path $FromPath -Parent
        if ($fromDir) {
            return (($fromDir -replace "\\", "/") + "/" + $candidate)
        }
    }

    return $candidate
}

function Get-EdgeKind {
    param([string]$Line, [string]$ToPath)
    if ($Line -match '(?i)source[- ]of[- ]truth|source of truth|current source') { return "declares_source" }
    if ($Line -match '(?i)context bundle|when to read|read before|see ') { return "routes_to" }
    if ($Line -match '(?i)mirror|mirrors') { return "mirrors" }
    if ($Line -match '(?i)extends|full checklist|related') { return "extends" }
    if ($Line -match '(?i)superseded|removed|historical|do not recreate') { return "supersedes" }
    if ($ToPath -match '^Assets/|^tools/') { return "routes_to" }
    return "references"
}

function Add-Diagnostic {
    param(
        [System.Collections.Generic.List[object]]$Diagnostics,
        [string]$Severity,
        [string]$Code,
        [string]$File,
        [Nullable[int]]$Line,
        [string]$Message
    )
    $Diagnostics.Add([ordered]@{
        severity = $Severity
        code = $Code
        file = $File
        line = $Line
        message = $Message
    })
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$scanFiles = New-Object System.Collections.Generic.List[string]
$required = @("AGENTS.md")
foreach ($path in $required) {
    $full = Join-Path $RepoRoot $path
    if (Test-Path $full) {
        $scanFiles.Add((Resolve-Path $full).Path)
    }
}
Get-ChildItem -Path (Join-Path $RepoRoot "docs") -Filter "*.md" -Recurse -File -ErrorAction SilentlyContinue |
    ForEach-Object { $scanFiles.Add($_.FullName) }
Get-ChildItem -Path (Join-Path $RepoRoot ".cursor/rules") -Filter "*.mdc" -File -ErrorAction SilentlyContinue |
    ForEach-Object { $scanFiles.Add($_.FullName) }

$scanFiles = @($scanFiles | Select-Object -Unique | Sort-Object)
$nodes = New-Object System.Collections.Generic.List[object]
$edges = New-Object System.Collections.Generic.List[object]
$diagnostics = New-Object System.Collections.Generic.List[object]
$existingPaths = @{}
$fileData = @{}

foreach ($file in $scanFiles) {
    $repoPath = ConvertTo-RepoPath $file
    $existingPaths[$repoPath] = $true
    $lines = Get-Content -Path $file
    $text = ($lines -join "`n")
    $type = Get-NodeType $repoPath
    $node = [ordered]@{
        id = $repoPath
        path = $repoPath
        title = Get-Title $lines ([System.IO.Path]::GetFileName($repoPath))
        type = $type
        status = $(if ($type -eq "historical_or_deprecated") { "historical" } else { "current" })
        ruleKinds = @(Get-RuleKinds $text $repoPath)
        summary = Get-Summary $repoPath $type
    }
    $nodes.Add($node)
    $fileData[$repoPath] = [ordered]@{ lines = $lines; text = $text; type = $type }
}

$historicalDocs = New-Object System.Collections.Generic.HashSet[string]
if ($fileData.Contains("docs/AGENT_CONTEXT.md")) {
    $lines = $fileData["docs/AGENT_CONTEXT.md"].lines
    $inHistorical = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^##\s+Removed / historical docs') { $inHistorical = $true; continue }
        if ($inHistorical -and $lines[$i] -match '^##\s+') { $inHistorical = $false }
        if ($inHistorical -and $lines[$i] -match '`(?<path>docs/[^`]+\.md)`') {
            $path = $Matches["path"]
            [void]$historicalDocs.Add($path)
            if (-not $existingPaths.ContainsKey($path)) {
                $nodes.Add([ordered]@{
                    id = $path
                    path = $path
                    title = [System.IO.Path]::GetFileName($path)
                    type = "historical_or_deprecated"
                    status = "removed"
                    ruleKinds = @("historical")
                    summary = "Removed or superseded document listed by docs/AGENT_CONTEXT.md."
                })
            }
        }
    }
}

$nodes.Add([ordered]@{
    id = "external:github-issue"
    path = $null
    title = "GitHub Issue Task Contract"
    type = "task_contract"
    status = "external"
    ruleKinds = @("workflow", "git")
    summary = "External issue body can become the task contract under the AGENTS.md GitHub Issue Workflow."
})

$referencePattern = '(?<ref>AGENTS\.md|docs[\\/][A-Za-z0-9._\/-]+\.md|\.cursor[\\/]rules[\\/][A-Za-z0-9._-]+\.mdc|Assets[\\/][A-Za-z0-9._\/-]+|tools[\\/][A-Za-z0-9._\/-]+|(?<![A-Za-z0-9_/.-])[A-Za-z0-9._-]+\.md)'

foreach ($fromPath in $fileData.Keys) {
    $lines = $fileData[$fromPath].lines
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($match in [regex]::Matches($line, $referencePattern)) {
            $toPath = Resolve-ReferencePath $match.Groups["ref"].Value $fromPath
            if ($toPath -eq $fromPath) { continue }
            $edges.Add([ordered]@{
                from = $fromPath
                to = $toPath
                kind = Get-EdgeKind $line $toPath
                line = $i + 1
                evidence = $line.Trim()
            })
        }
        if ($line -match '(?i)GitHub issue.*task contract|issue body.*task contract|issue.*contract') {
            $edges.Add([ordered]@{
                from = $fromPath
                to = "external:github-issue"
                kind = "external_contract"
                line = $i + 1
                evidence = $line.Trim()
            })
        }
    }
}

foreach ($path in $fileData.Keys) {
    $lines = $fileData[$path].lines
    $text = $fileData[$path].text

    if ($path -ne "AGENTS.md" -and $path -ne "docs/AGENT_CONTEXT.md" -and
        $text -match '(?i)\b(root authority|shared entry point|source[- ]of[- ]truth|authoritative)\b') {
        Add-Diagnostic $diagnostics "warning" "POSSIBLE_AUTHORITY_CLAIM" $path $null "This file uses authority/source-of-truth language. Confirm it is scoped beneath AGENTS.md."
    }

    if ($path -match '^\.cursor/rules/') {
        if ($text -match '(?i)AGENTS\.md.*source of truth|mirrors that policy|see `docs/TEST_HARNESS\.md`') {
            Add-Diagnostic $diagnostics "info" "TOOL_ADAPTER_MIRROR" $path $null "Cursor rule appears to mirror or specialize existing repository guidance."
        }
        if ($path -eq ".cursor/rules/feature-branching.mdc" -and $text -notmatch 'GitHub Issue Workflow') {
            Add-Diagnostic $diagnostics "warning" "MISSING_ISSUE_EXCEPTION" $path $null "Git workflow adapter does not mention the GitHub Issue Workflow exception."
        }
    }

    if ($path -eq "docs/PR_POST_OPEN_AGENT_LOOP.md") {
        Add-Diagnostic $diagnostics "info" "EXTENDS_AGENTS_WORKFLOW" $path $null "Post-PR loop explicitly extends AGENTS.md GitHub Issue Workflow."
    }

    if ($path -eq ".cursor/rules/controls-spec.mdc") {
        Add-Diagnostic $diagnostics "info" "DOMAIN_CANONICAL" $path $null "Controls rule is domain-canonical for camera/input behavior via docs/architecture.md."
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($doc in $historicalDocs) {
            if ($line -like "*$doc*" -and $path -ne "docs/AGENT_CONTEXT.md" -and $line -notmatch '(?i)historical|removed|superseded|do not recreate|not current') {
                Add-Diagnostic $diagnostics "warning" "HISTORICAL_DOC_REFERENCE" $path ($i + 1) "References removed or superseded document $doc outside explicit historical context."
            }
        }
        if ($line -match '(?i)\bmerge\b' -and $line -notmatch '(?i)never merge|do not merge|agents never merge|humans merge|human') {
            Add-Diagnostic $diagnostics "warning" "MERGE_RULE_REVIEW" $path ($i + 1) "Line mentions merging without an obvious human-only/no-agent-merge qualifier."
        }
        if ($line -match '(?i)\bcommit\b|\bpush\b|open a PR|pull request' -and $path -ne "AGENTS.md" -and $path -notmatch '^\.cursor/rules/feature-branching\.mdc$|^docs/PR_POST_OPEN_AGENT_LOOP\.md$|^docs/WORKTREE_BRANCH_HYGIENE\.md$|^docs/agent-pr-review-checklist\.md$') {
            Add-Diagnostic $diagnostics "info" "WORKFLOW_TERM" $path ($i + 1) "Workflow term found in a non-primary workflow file; review scope if this becomes policy."
        }
        if ($line -match '(?i)State Authority|FixedUpdateNetwork|UI.*must not mutate|serialized defaults|prefab|scene|\.meta|collider' -and $path -ne "AGENTS.md") {
            Add-Diagnostic $diagnostics "info" "TECHNICAL_RULE" $path ($i + 1) "Technical rule or constraint detected for graph classification."
        }
    }
}

$graph = [ordered]@{
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    banner = $Banner
    generator = "tools/instruction-graph/generate-instruction-graph.ps1"
    rootAuthority = "AGENTS.md"
    nodes = $nodes.ToArray()
    edges = $edges.ToArray()
    diagnostics = $diagnostics.ToArray()
}

$json = $graph | ConvertTo-Json -Depth 8
Set-Content -Path $JsonPath -Value $json -Encoding UTF8

$jsonForHtml = $json.Replace("<", "\u003c")
$htmlTemplate = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Instruction Graph Diagnostic</title>
  <style>
    :root { color-scheme: light dark; --border: #8a8f98; --muted: #667085; --accent: #2563eb; }
    body { font-family: Segoe UI, system-ui, sans-serif; margin: 0; line-height: 1.35; }
    header { padding: 16px 20px; border-bottom: 1px solid var(--border); background: #fff7ed; color: #111827; }
    header strong { display: block; margin-bottom: 4px; }
    main { display: grid; grid-template-columns: minmax(280px, 420px) 1fr; gap: 16px; padding: 16px; }
    input, select { width: 100%; box-sizing: border-box; margin: 6px 0 10px; padding: 8px; }
    .panel { border: 1px solid var(--border); border-radius: 6px; padding: 12px; overflow: auto; }
    .node, .diag, .edge { border-top: 1px solid var(--border); padding: 8px 0; }
    .pill { display: inline-block; padding: 1px 6px; border: 1px solid var(--border); border-radius: 999px; font-size: 12px; margin: 2px 4px 2px 0; }
    .warning { color: #b45309; }
    .error { color: #b91c1c; }
    .info { color: var(--accent); }
    svg { width: 100%; min-height: 520px; border: 1px solid var(--border); border-radius: 6px; background: Canvas; }
    text { font: 12px Segoe UI, system-ui, sans-serif; fill: CanvasText; }
    line { stroke: var(--border); stroke-width: 1.2; }
    circle { fill: var(--accent); }
    @media (max-width: 900px) { main { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <header>
    <strong>__BANNER__</strong>
    <span>Generated at <span id="generatedAt"></span> by tools/instruction-graph/generate-instruction-graph.ps1.</span>
  </header>
  <main>
    <section class="panel">
      <label>Search <input id="search" placeholder="Filter by path, title, type, rule kind"></label>
      <label>Node type <select id="typeFilter"><option value="">All types</option></select></label>
      <h2>Nodes</h2>
      <div id="nodes"></div>
    </section>
    <section>
      <div class="panel">
        <h2>Graph</h2>
        <svg id="graph" role="img" aria-label="Instruction file graph"></svg>
      </div>
      <div class="panel" style="margin-top:16px">
        <h2>Edges</h2>
        <div id="edges"></div>
      </div>
      <div class="panel" style="margin-top:16px">
        <h2>Diagnostics</h2>
        <div id="diagnostics"></div>
      </div>
    </section>
  </main>
  <script id="graph-data" type="application/json">__GRAPH_JSON__</script>
  <script>
    const graph = JSON.parse(document.getElementById('graph-data').textContent);
    const search = document.getElementById('search');
    const typeFilter = document.getElementById('typeFilter');
    document.getElementById('generatedAt').textContent = graph.generatedAt;

    for (const type of [...new Set(graph.nodes.map(n => n.type))].sort()) {
      const option = document.createElement('option');
      option.value = type;
      option.textContent = type;
      typeFilter.appendChild(option);
    }

    function matches(node) {
      const term = search.value.toLowerCase();
      const haystack = [node.id, node.title, node.type, node.status, ...(node.ruleKinds || [])].join(' ').toLowerCase();
      return (!typeFilter.value || node.type === typeFilter.value) && (!term || haystack.includes(term));
    }

    function render() {
      const visible = graph.nodes.filter(matches);
      const visibleIds = new Set(visible.map(n => n.id));
      document.getElementById('nodes').innerHTML = visible.map(n => `
        <div class="node">
          <strong>${n.id}</strong><br>
          <span class="pill">${n.type}</span><span class="pill">${n.status}</span>
          ${(n.ruleKinds || []).map(k => `<span class="pill">${k}</span>`).join('')}
          <div>${n.summary || ''}</div>
        </div>`).join('');

      document.getElementById('edges').innerHTML = graph.edges
        .filter(e => visibleIds.has(e.from) || visibleIds.has(e.to))
        .map(e => `<div class="edge"><strong>${e.from}</strong> -> <strong>${e.to}</strong> <span class="pill">${e.kind}</span><br><small>${e.line ? 'line ' + e.line + ': ' : ''}${e.evidence || ''}</small></div>`)
        .join('');

      document.getElementById('diagnostics').innerHTML = graph.diagnostics
        .filter(d => !typeFilter.value || visibleIds.has(d.file))
        .map(d => `<div class="diag ${d.severity}"><strong>[${d.severity}] ${d.code}</strong> ${d.file || ''}${d.line ? ':' + d.line : ''}<br>${d.message}</div>`)
        .join('');

      renderSvg(visible, graph.edges.filter(e => visibleIds.has(e.from) && visibleIds.has(e.to)));
    }

    function renderSvg(nodes, edges) {
      const svg = document.getElementById('graph');
      const width = Math.max(900, svg.clientWidth || 900);
      const row = 42;
      const height = Math.max(520, nodes.length * row + 40);
      svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
      const groups = [...new Set(nodes.map(n => n.type))].sort();
      const positions = new Map();
      const columns = new Map(groups.map((g, i) => [g, 80 + i * ((width - 160) / Math.max(1, groups.length - 1))]));
      const counts = new Map();
      for (const n of nodes) {
        const index = counts.get(n.type) || 0;
        counts.set(n.type, index + 1);
        positions.set(n.id, { x: columns.get(n.type), y: 40 + index * row });
      }
      const edgeMarkup = edges.map(e => {
        const a = positions.get(e.from), b = positions.get(e.to);
        return a && b ? `<line x1="${a.x}" y1="${a.y}" x2="${b.x}" y2="${b.y}"><title>${e.kind}</title></line>` : '';
      }).join('');
      const nodeMarkup = nodes.map(n => {
        const p = positions.get(n.id);
        const label = n.id.length > 44 ? '...' + n.id.slice(-41) : n.id;
        return `<g><circle cx="${p.x}" cy="${p.y}" r="6"><title>${n.title}</title></circle><text x="${p.x + 10}" y="${p.y + 4}">${label}</text></g>`;
      }).join('');
      svg.innerHTML = edgeMarkup + nodeMarkup;
    }

    search.addEventListener('input', render);
    typeFilter.addEventListener('change', render);
    render();
  </script>
</body>
</html>
'@

$html = $htmlTemplate.Replace("__BANNER__", [System.Web.HttpUtility]::HtmlEncode($Banner)).
    Replace("__GRAPH_JSON__", $jsonForHtml)

Set-Content -Path $HtmlPath -Value $html -Encoding UTF8
Write-Host "Wrote $JsonPath"
Write-Host "Wrote $HtmlPath"
