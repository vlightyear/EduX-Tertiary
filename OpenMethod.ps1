# -------------------------------
# Git: Open latest version of a method
# -------------------------------

param(
    [Parameter(Mandatory=$true)]
    [string]$MethodName,        # e.g., "calculateScore"

    [Parameter(Mandatory=$false)]
    [string]$FilePath            # optional: limit search to a specific file
)

# 1️⃣ Build git log command
$gitLogCmd = "git log -S`"$MethodName`" -1 --pretty=format:`"%H`""
if ($FilePath) {
    $gitLogCmd += " -- $FilePath"
}

# 2️⃣ Get the latest commit hash
$commitHash = Invoke-Expression $gitLogCmd

if (-not $commitHash) {
    Write-Host "Method '$MethodName' not found in any commit." -ForegroundColor Red
    exit
}

Write-Host "Latest commit where method exists: $commitHash" -ForegroundColor Green

# 3️⃣ Determine the file path
if (-not $FilePath) {
    # If no file path given, try to find the file automatically
    # List files changed in that commit
    $filesChanged = git show --pretty="" --name-only $commitHash
    if ($filesChanged.Count -eq 1) {
        $FilePath = $filesChanged[0]
        Write-Host "Found file: $FilePath" -ForegroundColor Yellow
    } else {
        Write-Host "Multiple files changed in this commit. Please specify the file path manually." -ForegroundColor Red
        exit
    }
}

# 4️⃣ Output the file content to a temporary location
$tempFile = Join-Path $env:TEMP ([IO.Path]::GetFileName($FilePath))
git show "$commitHash`:$FilePath" > $tempFile

# 5️⃣ Open the file in the default editor
Write-Host "Opening file: $tempFile" -ForegroundColor Cyan
Start-Process $tempFile
