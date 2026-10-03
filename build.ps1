$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:exe /optimize+ /out:"$PSScriptRoot\MonitorOsdControl.exe" "$PSScriptRoot\MonitorOsdControl.cs"
if ($LASTEXITCODE -ne 0) { throw "MonitorOsdControl build failed" }
& $csc /nologo /target:winexe /optimize+ /main:OsdLauncher /r:System.Windows.Forms.dll /out:"$PSScriptRoot\DisableOsd.exe" "$PSScriptRoot\MonitorOsdControl.cs" "$PSScriptRoot\OsdLauncher.cs"
if ($LASTEXITCODE -ne 0) { throw "DisableOsd build failed" }
Write-Host "Built $PSScriptRoot\DisableOsd.exe and MonitorOsdControl.exe"
