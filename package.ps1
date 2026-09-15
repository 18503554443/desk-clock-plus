$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $dir 'dist'
$out = Join-Path $dist 'DeskClockPlus.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Find-Asm($name) {
  foreach ($root in @('GAC_64','GAC_MSIL','GAC_32')) {
    $gac = Get-ChildItem "C:\Windows\Microsoft.NET\assembly\$root\$name" -Filter $name -Recurse -ErrorAction SilentlyContinue |
           Sort-Object FullName -Descending | Select-Object -First 1
    if ($gac) { return $gac.FullName }
  }
  $fw = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' $name
  if (Test-Path $fw) { return $fw }
  throw "assembly not found: $name"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') |
        ForEach-Object { '/r:' + (Find-Asm $_) }
$icon = Join-Path $dir 'app.ico'
if (!(Test-Path $icon)) { & (Join-Path $dir 'make-icon.ps1') | Out-Null }
$iconArgs = @(('/win32icon:' + $icon), ('/resource:' + $icon + ',AppIcon.ico'))

& $csc /nologo /noconfig /optimize+ /target:winexe /platform:anycpu /out:$out @iconArgs @refs (Join-Path $dir 'ClockApp.cs')
if ($LASTEXITCODE -ne 0) { throw 'package failed' }
"packaged: $out"
