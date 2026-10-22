# Compiles only the interop declarations in memory. Never invokes a native API.
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '..\MonitorBrightness\Native\PhysicalMonitorNative.cs'
$nativeTypes = Add-Type -TypeDefinition (Get-Content -LiteralPath $sourcePath -Raw) -PassThru
$nativeType = $nativeTypes | Where-Object FullName -EQ 'MonitorBrightness.Native.PhysicalMonitorNative'
$bindingFlags = [System.Reflection.BindingFlags]'NonPublic, Public, Static, Instance'

$expectedSizes = @{
    NativeRect = 16
    MonitorInfo = 104
    DisplayDevice = 840
    PhysicalMonitor = [IntPtr]::Size + 256
}
foreach ($typeName in $expectedSizes.Keys) {
    $structureType = $nativeType.GetNestedType($typeName, $bindingFlags)
    $actualSize = [System.Runtime.InteropServices.Marshal]::SizeOf([type]$structureType)
    if ($actualSize -ne $expectedSizes[$typeName]) {
        throw "$typeName size: expected $($expectedSizes[$typeName]), got $actualSize"
    }
    Write-Output "$typeName layout: PASS ($actualSize bytes)"
}

$expectedOffsets = @{
    MonitorInfo = @{ Monitor = 4; Work = 20; Flags = 36; DeviceName = 40 }
    DisplayDevice = @{ DeviceName = 4; Description = 68; StateFlags = 324; DeviceId = 328; DeviceKey = 584 }
    PhysicalMonitor = @{ Handle = 0; Description = [IntPtr]::Size }
}
foreach ($typeName in $expectedOffsets.Keys) {
    $structureType = $nativeType.GetNestedType($typeName, $bindingFlags)
    foreach ($fieldName in $expectedOffsets[$typeName].Keys) {
        $actualOffset = [System.Runtime.InteropServices.Marshal]::OffsetOf($structureType, $fieldName).ToInt32()
        if ($actualOffset -ne $expectedOffsets[$typeName][$fieldName]) {
            throw "Unexpected offset for $typeName.$fieldName : $actualOffset"
        }
    }
}
Write-Output 'Native field offsets: PASS'

$allowedImports = @(
    'EnumDisplayMonitors', 'GetMonitorInfoW', 'EnumDisplayDevicesW',
    'GetNumberOfPhysicalMonitorsFromHMONITOR', 'GetPhysicalMonitorsFromHMONITOR',
    'GetMonitorCapabilities', 'GetMonitorBrightness', 'DestroyPhysicalMonitor', 'SetMonitorBrightness'
)
$imports = @($nativeType.GetMethods($bindingFlags) | Where-Object {
    $_.GetCustomAttributes([System.Runtime.InteropServices.DllImportAttribute], $false).Count -gt 0
})
if (Compare-Object ($allowedImports | Sort-Object) ($imports.Name | Sort-Object)) {
    throw 'Native imports differ from the M3 allowlist.'
}
Write-Output 'Native import allowlist: PASS (8 enumeration/read/release APIs plus the M3 brightness write)'

$sdrPath = Join-Path $PSScriptRoot '..\MonitorBrightness\Native\DisplayConfigNative.cs'
$sdrTypes = Add-Type -TypeDefinition (Get-Content -LiteralPath $sdrPath -Raw) -PassThru
$sdrType = $sdrTypes | Where-Object FullName -EQ 'MonitorBrightness.Native.DisplayConfigNative'
$sdrSizes = @{
    Luid = 8; Header = 20; PathSource = 20; PathTarget = 48; DisplayPath = 72
    ModeInfo = 64; SourceName = 84; TargetName = 420; AdvancedColorInfo = 36
    WhiteLevel = 24; SetWhiteLevel = 28
}
foreach ($typeName in $sdrSizes.Keys) {
    $structureType = $sdrType.GetNestedType($typeName, $bindingFlags)
    $actualSize = [System.Runtime.InteropServices.Marshal]::SizeOf([type]$structureType)
    if ($actualSize -ne $sdrSizes[$typeName]) { throw "Unexpected SDR layout for $typeName : $actualSize" }
}
$sdrOffsets = @{
    Header = @{ Type = 0; Size = 4; Adapter = 8; Id = 16 }
    DisplayPath = @{ Source = 0; Target = 20; Flags = 68 }
    PathTarget = @{ OutputTechnology = 16; Available = 40; Status = 44 }
    SourceName = @{ Name = 20 }
    TargetName = @{ Name = 36; DevicePath = 164 }
    AdvancedColorInfo = @{ Flags = 20; ActiveColorMode = 32 }
    WhiteLevel = @{ Value = 20 }
    SetWhiteLevel = @{ Value = 20; Flag = 24 }
}
foreach ($typeName in $sdrOffsets.Keys) {
    $structureType = $sdrType.GetNestedType($typeName, $bindingFlags)
    foreach ($fieldName in $sdrOffsets[$typeName].Keys) {
        $actualOffset = [System.Runtime.InteropServices.Marshal]::OffsetOf($structureType, $fieldName).ToInt32()
        if ($actualOffset -ne $sdrOffsets[$typeName][$fieldName]) { throw "Unexpected SDR offset for $typeName.$fieldName : $actualOffset" }
    }
}
$sdrImports = @($sdrType.GetMethods($bindingFlags) | Where-Object {
    $_.GetCustomAttributes([System.Runtime.InteropServices.DllImportAttribute], $false).Count -gt 0
})
$sdrAllowed = @('GetDisplayConfigBufferSizes', 'QueryDisplayConfig', 'GetSourceName', 'GetTargetName',
    'GetAdvancedColorInfo', 'GetWhiteLevel', 'SetSdrWhiteLevel')
if (Compare-Object ($sdrAllowed | Sort-Object) ($sdrImports.Name | Sort-Object)) {
    throw 'SDR native imports differ from the approved M3 allowlist.'
}
Write-Output 'SDR layouts/offsets: PASS (11 structures; setter packet = 28 bytes)'
Write-Output 'SDR native import allowlist: PASS (no HDR toggle, gamma or topology setter)'
Write-Output 'No native functions or monitor commands were executed.'
