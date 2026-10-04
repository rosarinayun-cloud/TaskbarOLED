$ErrorActionPreference = 'Stop'

# Use Windows PowerShell 5.1 and the Windows-provided .NET Framework compiler.
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script with Windows PowerShell 5.1 (powershell.exe).'
}

$sourcePath = Join-Path $PSScriptRoot 'source\TaskbarOLED.cs'
$configurationPath = Join-Path $PSScriptRoot 'TaskbarOLED.ini'
$outputDirectory = Join-Path $PSScriptRoot 'dist'
$outputPath = Join-Path $outputDirectory 'TaskbarOLED.exe'
$outputConfigurationPath = Join-Path $outputDirectory 'TaskbarOLED.ini'

foreach ($inputPath in @($sourcePath, $configurationPath)) {
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        throw ('Required file is missing: ' + $inputPath)
    }
}

if (Test-Path -LiteralPath $outputPath) {
    throw 'dist\TaskbarOLED.exe already exists. Move the previous build before rebuilding.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Add-Type -TypeDefinition ([IO.File]::ReadAllText($sourcePath)) `
    -ReferencedAssemblies System.Windows.Forms,System.Drawing `
    -OutputAssembly $outputPath -OutputType WindowsApplication
if (-not (Test-Path -LiteralPath $outputConfigurationPath)) {
    Copy-Item -LiteralPath $configurationPath -Destination $outputConfigurationPath
}
Write-Output ('Built: ' + $outputPath)
