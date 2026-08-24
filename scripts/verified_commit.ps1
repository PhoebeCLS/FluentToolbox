param(
    [string]$Repo = $env:GITHUB_REPOSITORY,
    [string]$Branch = "main",
    [string]$Message = "chore(assets): auto-update UI preview screenshots [skip ci]",
    [string[]]$Files = @("assets/pdfdual_preview.jpg", "assets/iconcraft_preview.jpg")
)

if (-not $Repo) {
    $Repo = "PhoebeCLS/FluentToolbox"
}

# 1. Get current branch Head OID
$headOid = gh api "repos/$Repo/git/ref/heads/$Branch" --jq .object.sha
if (-not $headOid) {
    Write-Host "Warning: Could not get head OID for $Branch. Skipping verified commit."
    exit 0
}

# 2. Build additions array
$additions = @()
foreach ($file in $Files) {
    if (Test-Path $file) {
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $file))
        $b64 = [Convert]::ToBase64String($bytes)
        $cleanPath = $file.Replace('\', '/').TrimStart('./')
        $additions += @{
            path = $cleanPath
            contents = $b64
        }
    }
}

if ($additions.Count -eq 0) {
    Write-Host "No files to commit."
    exit 0
}

# 3. Create JSON payload for GraphQL createCommitOnBranch mutation
$payloadObj = @{
    query = 'mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid url } } }'
    variables = @{
        input = @{
            branch = @{
                repositoryNameWithOwner = $Repo
                branchName = $Branch
            }
            message = @{
                headline = $Message
            }
            fileChanges = @{
                additions = $additions
            }
            expectedHeadOid = $headOid
        }
    }
}

$jsonPayload = $payloadObj | ConvertTo-Json -Depth 10
$tmpJson = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmpJson, $jsonPayload, [System.Text.Encoding]::UTF8)

$commitResult = gh api graphql --input $tmpJson
Remove-Item $tmpJson -Force

Write-Host "Verified commit created successfully on GitHub:"
Write-Host $commitResult
