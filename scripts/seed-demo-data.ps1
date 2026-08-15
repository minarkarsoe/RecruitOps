<#
.SYNOPSIS
    Seeds demonstration data into a LOCAL RecruitOps dev stack.

.DESCRIPTION
    Drives the real HTTP API rather than writing SQL. That is deliberate: the approval
    chain only produces correct RequisitionApproval rows when a requisition actually goes
    through POST /submit and POST /decision. Hand-written SQL would give you demo data that
    renders correctly and is internally wrong -- inconsistent sequences, statuses that do not
    match their steps -- which is worse than no demo data, because it looks fine.

    Every approval below is posted by logging in AS that approver, so the sequential-turn
    rule is exercised, not bypassed.

.PARAMETER Force
    Re-seed even when demo data is already present. Creates duplicates; intended for a
    freshly reset database.

.NOTES
    LOCAL DEV ONLY. Creates real users with a shared, known password. Never point this at
    anything shared or production.

    This script is ASCII on purpose. Windows PowerShell 5.1 mis-decodes non-ASCII in a
    BOM-less .ps1, so all Myanmar text lives in demo-data.json and is read as UTF-8.
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [string]$BaseUrl
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------- env + dataset

$envPath = Join-Path $root '.env'
if (-not (Test-Path $envPath)) { throw ".env not found at $envPath. Copy .env.example to .env first." }

$envMap = @{}
foreach ($line in Get-Content $envPath -Encoding UTF8) {
    if ($line -match '^\s*#') { continue }
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
        $envMap[$Matches[1]] = $Matches[2].Trim()
    }
}

foreach ($required in @('SEED_ADMIN_EMAIL', 'SEED_ADMIN_PASSWORD')) {
    if (-not $envMap.ContainsKey($required) -or [string]::IsNullOrWhiteSpace($envMap[$required])) {
        throw "$required is not set in .env. The admin account this script logs in as is created from it on backend startup."
    }
}

if (-not $BaseUrl) {
    $port = '5080'
    if ($envMap.ContainsKey('API_PORT') -and $envMap['API_PORT']) { $port = $envMap['API_PORT'] }
    $BaseUrl = "http://localhost:$port"
}

$dataPath = Join-Path $PSScriptRoot 'demo-data.json'
if (-not (Test-Path $dataPath)) { throw "demo-data.json not found next to this script." }
$data = [System.IO.File]::ReadAllText($dataPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json

# ---------------------------------------------------------------- http helpers

function Invoke-Api {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body,
        [string]$Token
    )
    $headers = @{ 'Accept' = 'application/json' }
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }

    # Deliberately not named $args -- that is a PowerShell automatic variable and
    # assigning to it inside a function is a subtle way to break the splat.
    $call = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($null -ne $Body) {
        # UTF-8 bytes, not a string: Invoke-RestMethod would otherwise send Myanmar text
        # as Latin-1 and the names would arrive as mojibake.
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $call['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
        $call['ContentType'] = 'application/json; charset=utf-8'
    }
    return Invoke-RestMethod @call
}

function Get-Token {
    param([Parameter(Mandatory)][string]$Email, [Parameter(Mandatory)][string]$Password)
    $res = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $Email; password = $Password }
    return $res.accessToken
}

function Write-Step { param([string]$Text) Write-Host "  $Text" -ForegroundColor DarkGray }
function Write-Head { param([string]$Text) Write-Host "`n$Text" -ForegroundColor Cyan }

# ---------------------------------------------------------------- preflight

Write-Head "RecruitOps demo seed -> $BaseUrl"

try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/healthz" -Method GET -TimeoutSec 10
    Write-Step "API healthy (status: $($health.status))"
}
catch {
    throw "Cannot reach $BaseUrl/healthz. Start the stack first:  docker compose up -d"
}

$adminToken = Get-Token -Email $envMap['SEED_ADMIN_EMAIL'] -Password $envMap['SEED_ADMIN_PASSWORD']
Write-Step "Signed in as admin"

$existing = Invoke-Api -Method GET -Path '/api/departments' -Token $adminToken
if ($existing -and (@($existing) | Where-Object { $_.name -eq 'Engineering' }) -and -not $Force) {
    Write-Host "`nDemo data already present (found an 'Engineering' department)." -ForegroundColor Yellow
    Write-Host "Re-run with -Force to seed anyway (this will create duplicates)." -ForegroundColor Yellow
    return
}

$pw = $data.demoPassword
$deptIds = @{}; $userIds = @{}; $reqIds = @{}; $postingIds = @{}; $tokens = @{}

# ---------------------------------------------------------------- departments

Write-Head "Departments"
foreach ($d in $data.departments) {
    $created = Invoke-Api -Method POST -Path '/api/departments' -Token $adminToken -Body @{ name = $d.name; code = $d.code }
    $deptIds[$d.key] = $created.id
    Write-Step "$($d.name) [$($d.code)]"
}

# ---------------------------------------------------------------- users

Write-Head "Users (all share the password below)"
foreach ($u in $data.users) {
    $created = Invoke-Api -Method POST -Path '/api/users' -Token $adminToken -Body @{
        email = $u.email; displayName = $u.displayName; password = $pw; role = $u.role
    }
    $userIds[$u.key] = $created.id
    Write-Step "$($u.role.PadRight(14)) $($u.email)"
}

Write-Head "Department membership"
foreach ($d in $data.departments) {
    $members = @($data.users | Where-Object { $_.dept -eq $d.key } | ForEach-Object { $userIds[$_.key] })
    if ($members.Count -gt 0) {
        Invoke-Api -Method PUT -Path "/api/departments/$($deptIds[$d.key])/members" -Token $adminToken -Body @{ userIds = $members } | Out-Null
        Write-Step "$($d.name): $($members.Count) member(s)"
    }
}

# ---------------------------------------------------------------- approval chains

Write-Head "Approval chains"
foreach ($c in $data.approvalChains) {
    $steps = @($c.steps | ForEach-Object { @{ label = $_.label; approverUserId = $userIds[$_.user] } })
    $body = @{ name = $c.name; steps = $steps }
    if ($c.dept) { $body['departmentId'] = $deptIds[$c.dept] }
    $created = Invoke-Api -Method POST -Path '/api/approvalchains' -Token $adminToken -Body $body
    Write-Step "$($c.name) -- $(($c.steps | ForEach-Object { $_.label }) -join ' -> ')"
}

# ---------------------------------------------------------------- templates

# Both are RecruitmentStaff-gated, so they are created as a recruiter rather than as admin --
# that is who actually maintains them, and seeding as admin would not prove the role can.
$userTokens = @{}
function Get-UserToken {
    param([string]$Key)
    if (-not $userTokens.ContainsKey($Key)) {
        $u = $data.users | Where-Object { $_.key -eq $Key }
        $userTokens[$Key] = Get-Token -Email $u.email -Password $pw
    }
    return $userTokens[$Key]
}
$recruiterToken = Get-UserToken -Key 'rec1'

Write-Head "JD templates (Module 1.2)"
foreach ($t in $data.jdTemplates) {
    $body = @{ title = $t.title; content = $t.content }
    if ($t.dept) { $body['departmentId'] = $deptIds[$t.dept] }
    Invoke-Api -Method POST -Path '/api/jdtemplates' -Token $recruiterToken -Body $body | Out-Null
    $scope = 'all departments'
    if ($t.dept) { $scope = ($data.departments | Where-Object { $_.key -eq $t.dept }).name }
    Write-Step "$($t.title)  [$scope]"
}

Write-Head "Scorecard templates (Module 3)"
foreach ($s in $data.scorecardTemplates) {
    $criteria = @($s.criteria | ForEach-Object {
        @{ label = $_.label; guidance = $_.guidance; type = $_.type; isRequired = $_.isRequired }
    })
    $body = @{ name = $s.name; description = $s.description; isActive = $true; criteria = $criteria }
    if ($s.dept) { $body['departmentId'] = $deptIds[$s.dept] }
    Invoke-Api -Method POST -Path '/api/scorecardtemplates' -Token $recruiterToken -Body $body | Out-Null
    $scope = 'company-wide default'
    if ($s.dept) { $scope = ($data.departments | Where-Object { $_.key -eq $s.dept }).name }
    Write-Step "$($s.name)  [$scope, $($criteria.Count) criteria]"
}

# ---------------------------------------------------------------- requisitions

Write-Head "Requisitions (driven through the real approval flow)"
foreach ($r in $data.requisitions) {
    $reqToken = Get-UserToken -Key $r.requester
    $created = Invoke-Api -Method POST -Path '/api/requisitions' -Token $reqToken -Body @{
        departmentId   = $deptIds[$r.dept]
        title          = $r.title
        jobDescription = $r.jobDescription
        headcount      = $r.headcount
        salaryBudget   = $r.salaryBudget
    }
    $reqIds[$r.key] = $created.id
    $state = 'Draft'

    if ($r.submit) {
        Invoke-Api -Method POST -Path "/api/requisitions/$($created.id)/submit" -Token $reqToken | Out-Null
        $state = 'PendingApproval'

        foreach ($d in $r.decisions) {
            $approverToken = Get-UserToken -Key $d.by
            $after = Invoke-Api -Method POST -Path "/api/requisitions/$($created.id)/decision" -Token $approverToken -Body @{
                approve = $d.approve; comment = $d.comment
            }
            $state = $after.status
        }

        if ($r.cancel) {
            Invoke-Api -Method POST -Path "/api/requisitions/$($created.id)/cancel" -Token $reqToken | Out-Null
            $state = 'Cancelled'
        }
    }
    Write-Step "$($state.PadRight(16)) $($r.title)"
}

# ---------------------------------------------------------------- job postings

Write-Head "Job postings"
foreach ($p in $data.postings) {
    $req = $data.requisitions | Where-Object { $_.key -eq $p.requisition }
    $created = Invoke-Api -Method POST -Path '/api/jobpostings' -Token $recruiterToken -Body @{ requisitionId = $reqIds[$p.requisition] }
    $postingIds[$p.key] = $created.id

    Invoke-Api -Method PUT -Path "/api/jobpostings/$($created.id)" -Token $recruiterToken -Body @{
        title          = $req.title
        description    = $req.jobDescription
        location       = $p.location
        employmentType = 'FullTime'
        headcount      = $req.headcount
        salaryMin      = $p.salaryMin
        salaryMax      = $p.salaryMax
        showSalary     = $p.showSalary
    } | Out-Null

    if ($p.publish) {
        Invoke-Api -Method POST -Path "/api/jobpostings/$($created.id)/publish" -Token $recruiterToken | Out-Null
        Write-Step "Published: $($req.title) -- $($p.location)"
    }
}

# ------------------------------------------------- portal tokens (read from DB)

# GET /api/portal is a stub returning [] (PortalController.cs), so there is no API path to
# the public link. Publishing mints it in JobPostingService, so read it straight from Postgres.
Write-Head "Public application links"
$dbUser = 'postgres'; $dbName = 'recruitops'
if ($envMap.ContainsKey('POSTGRES_USER') -and $envMap['POSTGRES_USER']) { $dbUser = $envMap['POSTGRES_USER'] }
if ($envMap.ContainsKey('POSTGRES_DB')   -and $envMap['POSTGRES_DB'])   { $dbName = $envMap['POSTGRES_DB'] }

foreach ($p in $data.postings) {
    if (-not $p.publish) { continue }
    # Piped over stdin, NOT passed as -c "<sql>". PowerShell strips the double quotes when
    # handing an argument to a native exe, so the quoted PascalCase identifiers arrive
    # unquoted, Postgres folds them to lowercase, and you get
    # 'relation "portallinks" does not exist' against a table that plainly exists.
    $sql = 'SELECT "Token" FROM "PortalLinks" WHERE "JobPostingId" = ''' + $postingIds[$p.key] + ''' LIMIT 1;'
    $out = $sql | docker compose exec -T db psql -U $dbUser -d $dbName -t -A 2>$null
    $tok = ($out | Out-String).Trim()
    if ($tok) {
        $tokens[$p.key] = $tok
        Write-Step "$($p.key): $BaseUrl/api/public/jobs/$tok"
    }
    else {
        Write-Host "  ! Could not read the portal token for $($p.key) - candidates for it will be skipped." -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------- candidates

Write-Head "Candidates (submitted through the real public apply endpoint)"
foreach ($c in $data.candidates) {
    if (-not $tokens.ContainsKey($c.posting)) { continue }
    Invoke-Api -Method POST -Path "/api/public/jobs/$($tokens[$c.posting])/apply" -Body @{
        fullName = $c.fullName; email = $c.email; phone = $c.phone; coverNote = $c.coverNote
    } | Out-Null
    Write-Step "$($c.fullName) -> $($c.posting)"
}

# SubmitApplicationResponse is deliberately just a message (SubmitApplicationResponse.cs) --
# a public, unauthenticated endpoint should not hand an anonymous caller an internal id. So the
# application ids for the pipeline steps below must be looked up, not read from the response.
$appSql = 'SELECT c."FullName" || ''|'' || a."Id" FROM "JobApplications" a JOIN "Candidates" c ON c."Id" = a."CandidateId";'
$appRows = $appSql | docker compose exec -T db psql -U $dbUser -d $dbName -t -A 2>$null
$appIds = @{}
foreach ($row in ($appRows -split "`n")) {
    $line = $row.Trim(); if (-not $line) { continue }
    $parts = $line -split '\|', 2
    $appIds[$parts[0]] = $parts[1]
}
Write-Step "Resolved $($appIds.Count) application id(s)"

Write-Head "Pipeline stages"
foreach ($c in $data.candidates) {
    if ($c.stage -eq 'Applied' -or -not $appIds.ContainsKey($c.fullName)) { continue }
    try {
        Invoke-Api -Method POST -Path "/api/applications/$($appIds[$c.fullName])/stage" -Token $recruiterToken -Body @{
            toStatus = $c.stage; note = "Moved to $($c.stage) during demo seeding."
        } | Out-Null
        Write-Step "$($c.stage.PadRight(12)) $($c.fullName)"
    }
    catch {
        Write-Host "  ! Could not move $($c.fullName) to $($c.stage): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------- interviews

Write-Head "Interviews"
foreach ($i in $data.interviews) {
    if (-not $appIds.ContainsKey($i.candidate)) { continue }
    $panel = @($i.panel | ForEach-Object { $userIds[$_] })
    try {
        $start = (Get-Date).ToUniversalTime().AddDays($i.daysFromNow).ToString('o')
        Invoke-Api -Method POST -Path "/api/applications/$($appIds[$i.candidate])/interviews" -Token $recruiterToken -Body @{
            scheduledStart      = $start
            durationMinutes     = $i.durationMinutes
            mode                = 'OnSite'
            location            = 'Head office, Yangon'
            agenda              = $i.title
            participantUserIds  = $panel
            leadUserId          = $panel[0]
        } | Out-Null
        Write-Step "$($i.title) for $($i.candidate) (in $($i.daysFromNow) days)"
    }
    catch {
        Write-Host "  ! Could not schedule '$($i.title)' for $($i.candidate): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------- scorecards

# Interview ids are not returned in a shape this script tracked, so resolve by agenda.
$interviewIds = @{}
$ivSql = 'SELECT i."Agenda" || ''|'' || i."Id" FROM "Interviews" i;'
foreach ($row in (($ivSql | docker compose exec -T db psql -U $dbUser -d $dbName -t -A 2>$null) -split "`n")) {
    $line = $row.Trim(); if (-not $line) { continue }
    $parts = $line -split '\|', 2
    $interviewIds[$parts[0]] = $parts[1]
}

Write-Head "Scorecards"
foreach ($sc in $data.scorecards) {
    if (-not $interviewIds.ContainsKey($sc.interview)) { continue }
    $iid = $interviewIds[$sc.interview]
    $token = Get-UserToken -Key $sc.by
    $who = ($data.users | Where-Object { $_.key -eq $sc.by }).displayName
    try {
        # Criterion ids only exist once the template is created, and the interview snapshots
        # its template at scheduling time -- which is why templates are seeded before
        # requisitions above. Seed them after, and every interview gets a null template and
        # no scorecard can ever be filled in.
        $own = Invoke-Api -Method GET -Path "/api/interviews/$iid/scorecard" -Token $token
        $critByLabel = @{}
        foreach ($c in $own.criteria) { $critByLabel[$c.label] = $c.id }
        if ($critByLabel.Count -eq 0) {
            Write-Host "  ! $who : the interview has no scorecard template" -ForegroundColor Yellow
            continue
        }

        $answers = @()
        foreach ($a in $sc.answers) {
            if (-not $critByLabel.ContainsKey($a.label)) { continue }
            $entry = @{ scorecardCriterionId = $critByLabel[$a.label] }
            if ($null -ne $a.rating) { $entry['rating'] = $a.rating }
            if ($null -ne $a.yesNo)  { $entry['yesNo']  = $a.yesNo }
            if ($a.comment)          { $entry['comment'] = $a.comment }
            $answers += $entry
        }

        $scBody = @{ summaryComment = $sc.summaryComment; answers = $answers }
        if ($sc.recommendation) { $scBody['recommendation'] = $sc.recommendation }
        Invoke-Api -Method PUT -Path "/api/interviews/$iid/scorecard" -Token $token -Body $scBody | Out-Null

        $state = 'Draft'
        if ($sc.submit) {
            # Submit takes a full SaveScorecardRequest and rebuilds its completeness check from
            # THIS request (ScorecardService.cs:133) -- it does not read the saved draft. An
            # empty body here always fails with "Answer the required criteria before submitting".
            Invoke-Api -Method POST -Path "/api/interviews/$iid/scorecard/submit" -Token $token -Body $scBody | Out-Null
            $state = "Submitted ($($sc.recommendation))"
        }
        Write-Step "$state -- $who on '$($sc.interview)' ($($answers.Count) answers)"
    }
    catch { Write-Host "  ! $who on '$($sc.interview)': $($_.Exception.Message)" -ForegroundColor Yellow }
}

# ---------------------------------------------------------------- notes

Write-Head "Notes (@mentions resolve from the email local part)"
foreach ($n in $data.notes) {
    if (-not $appIds.ContainsKey($n.candidate)) { continue }
    $token = Get-UserToken -Key $n.by
    $noteBody = @{ body = $n.body }
    if ($n.pinToInterview -and $interviewIds.ContainsKey($n.pinToInterview)) {
        $noteBody['interviewId'] = $interviewIds[$n.pinToInterview]
    }
    try {
        Invoke-Api -Method POST -Path "/api/applications/$($appIds[$n.candidate])/notes" -Token $token -Body $noteBody | Out-Null
        $who = ($data.users | Where-Object { $_.key -eq $n.by }).displayName
        $pin = ''
        if ($n.pinToInterview) { $pin = " [pinned to '$($n.pinToInterview)']" }
        Write-Step "$($n.candidate) <- $who$pin"
    }
    catch { Write-Host "  ! note on $($n.candidate): $($_.Exception.Message)" -ForegroundColor Yellow }
}

# ---------------------------------------------------------------- summary

Write-Host "`nDone." -ForegroundColor Green
Write-Host "`nSign in at http://localhost:5173 with any of these -- password: $pw" -ForegroundColor White
foreach ($u in $data.users) {
    Write-Host ("  {0,-14} {1,-34} {2}" -f $u.role, $u.email, $u.displayName)
}
Write-Host "`nFor the approval demo, sign in as one of:" -ForegroundColor White
Write-Host "  u myo set paing (Engineering Head) -- has 2 requisitions waiting in Inbox"
Write-Host "  u thein tun     (Finance)          -- has 1 requisition waiting in Inbox"
Write-Host "`nRequisitions cover every state: Draft, PendingApproval (step 1 and step 2), Approved, Rejected, Cancelled." -ForegroundColor White
