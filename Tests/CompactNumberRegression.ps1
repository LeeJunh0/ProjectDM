$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Systems/UI/Common/CompactNumber.cs') -Raw
$numberType = Add-Type -TypeDefinition $source -PassThru | Where-Object FullName -eq 'ProjectDM.CompactNumber'
$format = $numberType.GetMethod('Format', [Reflection.BindingFlags]'NonPublic,Static')
$cases = @(
    @(0, '0'), @(1, '1'), @(12.34, '12.34'), @(999, '999'), @(999.999, '999.99'),
    @(1000, '1,000'), @(1250, '1,250'), @(10000, '10,000'), @(100000, '100,000'),
    @(999999, '999,999'), @(999999.999, '999,999.99'), @(1000000, '1M'), @(1250000, '1.25M'), @(100000000, '100M'),
    @(999999999, '999.99M'), @(1000000000, '1B'), @(2147483647, '2.14B'),
    @(1000000000000, '1T'), @(1250000000000, '1.25T'), @(-1250, '-1,250'),
    @(-0.001, '0'), @([double]::NaN, '—'), @([double]::PositiveInfinity, '—')
)
$culture = [Globalization.CultureInfo]::CurrentCulture
$checks = 0
try {
    foreach ($locale in @('ko-KR', 'en-US', 'de-DE')) {
        [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo($locale)
        foreach ($case in $cases) {
            $actual = $format.Invoke($null, [object[]]@([double]$case[0]))
            if ($actual -cne $case[1]) { throw "Unexpected compact number ($locale): $($case[0]) -> $actual, expected $($case[1])" }
            $checks++
        }
    }
} finally { [Globalization.CultureInfo]::CurrentCulture = $culture }
Write-Output "Compact numbers: $checks assertions PASS (no K, full grouped numbers below M, M/B/T boundaries, no upward rounding, fractions, int cap, stable separators across locales)."
