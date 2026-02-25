# -------------------------------
# Git: List files containing a method and open latest version
# -------------------------------

param(
    [Parameter(Mandatory=$false)]
    [string]$MethodName
)

# 1️⃣ Ask for method name if not provided
if (-not $MethodName) {
    $MethodName = Read-Host "Enter the method name to search for"
}

# 2️⃣ Get all commits where the method exists
$commits = git log -S"$MethodName" --pretty=format:"%H %ad" --date=short --all

if (-not $commits) {
    Write-Host "No commits found containing method '$MethodName'." -ForegroundColor Red
    exit
}

# 3️⃣ Build a list of files with their latest commit date
$fileDict = @{}

foreach ($line in $commits) {
    $parts = $line -split " "
    $commitHash = $parts[0]
    $commitDate = $parts[1]

    # Get all files changed in this commit
    $files = git show --pretty="" --name-only $commitHash

    foreach ($file in $files) {
        # Keep the latest date only
        if (-not $fileDict.ContainsKey($file) -or $commitDate -gt $fileDict[$file].Date) {
            $fileDict[$file] = [PSCustomObject]@{
                Commit = $commitHash
                Date = $commitDate
            }
        }
    }
}

# 4️⃣ Display the list
Write-Host "`nFiles containing '$MethodName':" -ForegroundColor Cyan
$i = 1
$fileList = $fileDict.Keys | Sort-Object
foreach ($file in $fileList) {
    $info = $fileDict[$file]
    Write-Host "$i. $file  (Latest commit: $($info.Date))"
    $i++
}

# 5️⃣ Ask user to select a file to open
$selection = Read-Host "`nEnter the number of the file you want to open"
if (-not [int]::TryParse($selection, [ref]$null) -or $selection -lt 1 -or $selection -gt $fileList.Count) {
    Write-Host "Invalid selection." -ForegroundColor Red
    exit
}

$selectedFile = $fileList[$selection - 1]
$commitHash = $fileDict[$selectedFile].Commit

# 6️⃣ Output the file to temp and open
$tempFile = Join-Path $env:TEMP ([IO.Path]::GetFileName($selectedFile))
git show "$commitHash`:$selectedFile" > $tempFile
Write-Host "`nOpening file: $tempFile from commit $commitHash" -ForegroundColor Green
Start-Process $tempFile
