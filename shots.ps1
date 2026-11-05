
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W6 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
}
"@
[W6]::SetProcessDpiAwareness(2) | Out-Null
$dir = Join-Path (Get-Location) 'dist'
function Shot($name) {
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
  $bmp.Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}
function ClickAt($x, $y) {
  [W6]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 300
  [W6]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 130
  [W6]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 1800
}
$exe = Join-Path $env:LOCALAPPDATA 'Programs\GTACoop Remake\GTACoop.Launcher.exe'
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 8
$p.Refresh()
$h = $p.MainWindowHandle
[W6]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 900
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$items = @(
  @('p2-bilesenler.png', 'Bileşenler'), @('p4-tarayici.png', 'Sunucu Tarayıcı'),
  @('p7-gunluk.png', 'Günlük / Araçlar'), @('p8-hakkinda.png', 'Hakkında')
)
foreach ($it in $items) {
  $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
  $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $it[1])
  $cond = New-Object System.Windows.Automation.AndCondition($c1, $c2)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if ($el -eq $null) { Write-Output ("MISS " + $it[1]); continue }
  $rc = $el.Current.BoundingRectangle
  ClickAt ([int]($rc.X + $rc.Width / 2)) ([int]($rc.Y + $rc.Height / 2))
  Shot $it[0]
  Write-Output ("shot " + $it[0])
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
