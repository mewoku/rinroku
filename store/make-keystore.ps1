<#
.SYNOPSIS
  Creates the ODLET release signing key (Android keystore) OUTSIDE the repository.

.DESCRIPTION
  Uses keytool from Unity's bundled OpenJDK. Prompts for the password (never on the command line, never
  written to disk by this script). The keystore is PKCS12, so the key password equals the store password.

  Losing this file or its password means you can never publish an update to the same app listing.
  Back it up (password manager + an offline copy) before building anything with it.

  The Solana dApp Store requires a key that is NOT used for Google Play. If you later publish to Play,
  let Play App Signing hold its own key (upload a separate upload key) — do not reuse this one there.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File store\make-keystore.ps1
  powershell -ExecutionPolicy Bypass -File store\make-keystore.ps1 -OutFile D:\keys\ronriku-dappstore.keystore
#>
param(
  [string]$OutFile = (Join-Path $HOME "ronriku-keys\ronriku-dappstore.keystore"),
  [string]$Alias = "ronriku",
  [string]$DName = "CN=ODLET, O=ODLET",
  [string]$Keytool = "D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
)
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Keytool)) { throw "keytool not found at $Keytool (pass -Keytool <path>)." }

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outFull = [System.IO.Path]::GetFullPath($OutFile)
if ($outFull.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to create the keystore inside the repository ($repo). Choose a path outside it."
}
if (Test-Path $outFull) { throw "$outFull already exists. Refusing to overwrite a signing key." }
New-Item -ItemType Directory -Force (Split-Path $outFull) | Out-Null

function Read-Secret([string]$prompt) {
  $secure = Read-Host -AsSecureString $prompt
  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
  finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

$pass = Read-Secret "Keystore password (min 12 chars)"
$again = Read-Secret "Repeat password"
if ($pass -ne $again) { throw "Passwords do not match." }
if ($pass.Length -lt 12) { throw "Use at least 12 characters." }

# keytool reads the passwords from environment variables (:env), so they never appear in the process list.
$env:RONRIKU_KT_PASS = $pass
try {
  & $Keytool -genkeypair -v `
    -keystore $outFull -storetype PKCS12 `
    -alias $Alias -keyalg RSA -keysize 4096 -validity 10000 `
    -dname $DName `
    -storepass:env RONRIKU_KT_PASS -keypass:env RONRIKU_KT_PASS
  if ($LASTEXITCODE -ne 0) { throw "keytool failed ($LASTEXITCODE)." }

  Write-Host ""
  Write-Host "Certificate fingerprints (record the SHA-256 for your notes):"
  & $Keytool -list -v -keystore $outFull -alias $Alias -storepass:env RONRIKU_KT_PASS |
    Select-String -Pattern "SHA256:|Valid from|Owner:"
}
finally {
  Remove-Item Env:\RONRIKU_KT_PASS -ErrorAction SilentlyContinue
  $pass = $null; $again = $null
}

Write-Host ""
Write-Host "Created $outFull (alias '$Alias')."
Write-Host "BACK IT UP NOW: copy the file + password to a password manager and an offline drive."
Write-Host ""
Write-Host "Before a release build, in the SAME shell that launches Unity (the build reads these at build time):"
Write-Host "  `$env:RONRIKU_KEYSTORE   = '$outFull'"
Write-Host "  `$env:RONRIKU_KEY_ALIAS  = '$Alias'"
Write-Host "  `$env:RONRIKU_KEYSTORE_PASS = Read-Host 'store pass'   # PKCS12: key pass = store pass"
Write-Host "  `$env:RONRIKU_KEY_PASS      = `$env:RONRIKU_KEYSTORE_PASS"
Write-Host "Then start Unity from that shell and run RONRIKU > Build Android (Release)."
