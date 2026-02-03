$ErrorActionPreference = "Stop"

$dllPath = "C:\VCamNet\VCamNetSampleSource.comhost.dll"
$clsid = "{B624D1C1-4F7D-4646-AAC0-1A51CD4CEDD7}"
$categoryGuid = "{860BB310-5D01-11d0-BD3B-00A0C911CE86}" # Video Input Device Category
$friendlyName = "VCamNet Sample Camera"

Write-Host "Registering COM DLL..."
Start-Process regsvr32 -ArgumentList "/s `"$dllPath`"" -Wait

Write-Host "Adding Camera Category Registration..."

# The registry path for the category instance
# HKLM\SOFTWARE\Classes\CLSID\{Category}\Instance\{CameraCLSID}
$path = "HKLM:\SOFTWARE\Classes\CLSID\$categoryGuid\Instance\$clsid"
$path64 = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$categoryGuid\Instance\$clsid"

# Ensure 64-bit registration (Standard)
if (-not (Test-Path $path)) {
    New-Item -Path $path -Force | Out-Null
}
Set-ItemProperty -Path $path -Name "FriendlyName" -Value $friendlyName
Set-ItemProperty -Path $path -Name "CLSID" -Value $clsid
Set-ItemProperty -Path $path -Name "FilterData" -Value ([byte[]](0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00))

# Ensure 32-bit registration (WOW64) - for 32-bit apps using the camera
if (-not (Test-Path $path64)) {
    New-Item -Path $path64 -Force | Out-Null
}
Set-ItemProperty -Path $path64 -Name "FriendlyName" -Value $friendlyName
Set-ItemProperty -Path $path64 -Name "CLSID" -Value $clsid
Set-ItemProperty -Path $path64 -Name "FilterData" -Value ([byte[]](0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00))

Write-Host "Camera Registered Successfully!"
