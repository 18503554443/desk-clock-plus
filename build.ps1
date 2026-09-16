$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
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

$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Management.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') |
        ForEach-Object { '/r:' + (Find-Asm $_) }
$icon = Join-Path $dir 'app.ico'
$manifest = Join-Path $dir 'app.manifest'
$libre = Join-Path $dir 'LibreHardwareMonitorLib.dll'
$hid = Join-Path $dir 'HidSharp.dll'
if (!(Test-Path $icon)) { & (Join-Path $dir 'make-icon.ps1') | Out-Null }
$iconArgs = @(('/win32icon:' + $icon), ('/resource:' + $icon + ',AppIcon.ico'))
$libArgs = @(
  ('/r:' + $libre),
  ('/r:' + $hid),
  ('/resource:' + $libre + ',LibreHardwareMonitorLib.dll'),
  ('/resource:' + $hid + ',HidSharp.dll')
)

$out = Join-Path $dir 'DesktopClock.exe'
& $csc /nologo /noconfig /target:winexe /platform:anycpu /out:$out /win32manifest:$manifest @iconArgs @libArgs @refs (Join-Path $dir 'ClockApp.cs')
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
"built: $out"
