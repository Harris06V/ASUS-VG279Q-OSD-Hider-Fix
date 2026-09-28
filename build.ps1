$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /optimize+ /out:"$PSScriptRoot\OsdHider.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$PSScriptRoot\OsdHider.cs"
if ($LASTEXITCODE -eq 0) { Write-Host "Built $PSScriptRoot\OsdHider.exe" }
