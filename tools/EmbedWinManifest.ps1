param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ManifestEmbedder {
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr BeginUpdateResource(string fileName, bool deleteExisting);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool EndUpdateResource(IntPtr update, bool discard);
}
"@

$exe = (Resolve-Path $ExePath).Path
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $ManifestPath).Path)
$handle = [ManifestEmbedder]::BeginUpdateResource($exe, $false)
if ($handle -eq [IntPtr]::Zero) {
    throw "BeginUpdateResource failed for $exe (Win32 $($([Runtime.InteropServices.Marshal]::GetLastWin32Error())))"
}

# RT_MANIFEST = 24, resource id 1, language-neutral
$updated = [ManifestEmbedder]::UpdateResource($handle, [IntPtr]24, [IntPtr]1, [uint16]0, $bytes, [uint32]$bytes.Length)
if (-not $updated) {
    [ManifestEmbedder]::EndUpdateResource($handle, $true) | Out-Null
    throw "UpdateResource failed for $exe (Win32 $($([Runtime.InteropServices.Marshal]::GetLastWin32Error())))"
}

if (-not [ManifestEmbedder]::EndUpdateResource($handle, $false)) {
    throw "EndUpdateResource failed for $exe (Win32 $($([Runtime.InteropServices.Marshal]::GetLastWin32Error())))"
}

Write-Host "Embedded Windows compatibility manifest into $exe"
