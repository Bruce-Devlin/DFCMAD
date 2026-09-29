param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\DFCMAD.App\DFCMAD.App.csproj"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  $OutputPath = Join-Path $repoRoot "artifacts\publish\$Runtime"
}

dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  -o $OutputPath `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false
