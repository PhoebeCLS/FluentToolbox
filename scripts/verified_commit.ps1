param(
    [string]$Repo = "PhoebeCLS/FluentToolbox",
    [string]$Branch = "main",
    [string]$Message = "chore(assets): auto-update UI preview screenshots [skip ci]",
    [string[]]$Files = @("assets/pdfdual_preview.jpg", "assets/iconcraft_preview.jpg")
)

# 1. Get Head Commit SHA and Tree SHA
$headRef = gh api "repos/$Repo/git/ref/heads/$Branch" | ConvertFrom-Json
$headCommitSha = $headRef.object.sha
$headCommit = gh api "repos/$Repo/git/commits/$headCommitSha" | ConvertFrom-Json
$baseTreeSha = $headCommit.tree.sha

Write-Host "Head Commit: $headCommitSha, Base Tree: $baseTreeSha"

# 2. Upload Blobs
$treeEntries = @()
foreach ($file in $Files) {
    if (Test-Path $file) {
        $cleanPath = $file.Replace('\', '/').TrimStart('./')
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $file))
        $b64 = [Convert]::ToBase64String($bytes)
        
        $blobPayload = @{
            content = $b64
            encoding = "base64"
        } | ConvertTo-Json
        
        $tmpBlob = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tmpBlob, $blobPayload, [System.Text.Encoding]::UTF8)
        $blobRes = gh api "repos/$Repo/git/blobs" --input $tmpBlob | ConvertFrom-Json
        Remove-Item $tmpBlob -Force
        
        Write-Host "Uploaded blob for ${cleanPath}: $($blobRes.sha)"
        
        $treeEntries += @{
            path = $cleanPath
            mode = "100644"
            type = "blob"
            sha = $blobRes.sha
        }
    }
}

if ($treeEntries.Count -eq 0) {
    Write-Host "No files found to commit."
    exit 0
}

# 3. Create Tree
$treePayload = @{
    base_tree = $baseTreeSha
    tree = $treeEntries
} | ConvertTo-Json -Depth 5

$tmpTree = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmpTree, $treePayload, [System.Text.Encoding]::UTF8)
$treeRes = gh api "repos/$Repo/git/trees" --input $tmpTree | ConvertFrom-Json
Remove-Item $tmpTree -Force
Write-Host "Created Tree: $($treeRes.sha)"

# 4. Create Commit
$commitPayload = @{
    message = $Message
    tree = $treeRes.sha
    parents = @($headCommitSha)
} | ConvertTo-Json

$tmpCommit = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmpCommit, $commitPayload, [System.Text.Encoding]::UTF8)
$commitRes = gh api "repos/$Repo/git/commits" --input $tmpCommit | ConvertFrom-Json
Remove-Item $tmpCommit -Force
Write-Host "Created Verified Commit: $($commitRes.sha)"

# 5. Update Branch Ref
$refPayload = @{
    sha = $commitRes.sha
    force = $false
} | ConvertTo-Json

$tmpRef = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmpRef, $refPayload, [System.Text.Encoding]::UTF8)
$updateRefRes = gh api -X PATCH "repos/$Repo/git/refs/heads/$Branch" --input $tmpRef | ConvertFrom-Json
Remove-Item $tmpRef -Force
Write-Host "Updated $Branch ref to $($commitRes.sha) successfully!"

# 6. Check verification status of this commit
$verification = gh api "repos/$Repo/commits/$($commitRes.sha)" | ConvertFrom-Json
Write-Host "Verification Status: $($verification.commit.verification.verified), Reason: $($verification.commit.verification.reason)"
