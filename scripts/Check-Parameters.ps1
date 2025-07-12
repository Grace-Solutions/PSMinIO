# Check what parameters are available for New-MinIOObject
Import-Module ".\Module\PSMinIO\PSMinIO.psd1" -Force

Write-Host "=== New-MinIOObject Parameter Information ===" -ForegroundColor Cyan

# Get command info
$cmdInfo = Get-Command New-MinIOObject

Write-Host "`nParameter Sets:" -ForegroundColor Yellow
foreach ($paramSet in $cmdInfo.ParameterSets) {
    Write-Host "  $($paramSet.Name):" -ForegroundColor Green
    foreach ($param in $paramSet.Parameters) {
        if ($param.Name -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction', 'ErrorVariable', 'WarningVariable', 'InformationVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable', 'WhatIf', 'Confirm')) {
            $mandatory = if ($param.IsMandatory) { " (Mandatory)" } else { "" }
            $aliases = if ($param.Aliases.Count -gt 0) { " [Aliases: $($param.Aliases -join ', ')]" } else { "" }
            Write-Host "    - $($param.Name)$mandatory$aliases" -ForegroundColor White
        }
    }
    Write-Host ""
}

Write-Host "All Parameters:" -ForegroundColor Yellow
foreach ($param in $cmdInfo.Parameters.Values) {
    if ($param.Name -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction', 'ErrorVariable', 'WarningVariable', 'InformationVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable', 'WhatIf', 'Confirm')) {
        $aliases = if ($param.Aliases.Count -gt 0) { " [Aliases: $($param.Aliases -join ', ')]" } else { "" }
        $paramSets = $param.ParameterSets.Keys -join ', '
        Write-Host "  $($param.Name)$aliases - Sets: $paramSets" -ForegroundColor White
    }
}
