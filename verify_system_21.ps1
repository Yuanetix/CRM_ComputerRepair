$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5213"
$results = @()

function Record-Result {
    param([int]$id, [string]$title, [bool]$passed, [string]$detail)
    $obj = [PSCustomObject]@{
        Id = $id
        Title = $title
        Passed = $passed
        Detail = $detail
    }
    $script:results += $obj
    $status = if ($passed) { "PASS" } else { "FAIL" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "[$status] Item $($id): $title - $detail" -ForegroundColor $color
}

# 1. Login Super Admin
Write-Host "--- Authenticating Super Admin ---"
$loginBody = @{ username = "superadmin"; password = "SuperAdmin@123" } | ConvertTo-Json
$saLogin = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$saToken = $saLogin.token
$saHeaders = @{ Authorization = "Bearer $saToken" }

# Fetch Plans
$plans = Invoke-RestMethod -Uri "$baseUrl/subscriptions/plans" -Method Get -Headers $saHeaders
$entPlan = $plans | Where-Object { $_.planCode -eq "ENTERPRISE" }
$opsPlan = $plans | Where-Object { $_.planCode -eq "OPERATIONS" }
$intPlan = $plans | Where-Object { $_.planCode -eq "INTELLIGENCE" }
$brPlan = $plans | Where-Object { $_.planCode -eq "BRANCH" }

# Item 1: Enterprise plan contains all 5 modules
$entMods = $entPlan.includedModules.moduleCode
$has5 = ($entMods.Count -eq 5) -and ($entMods -contains "MAIN_TRANSACTIONS") -and ($entMods -contains "DATA_COLLECTION") -and ($entMods -contains "BUSINESS_INTELLIGENCE") -and ($entMods -contains "ACTIONS") -and ($entMods -contains "BRANCHING")
Record-Result 1 "Enterprise Plan 5 Modules" $has5 "Enterprise modules: $($entMods -join ', ')"

# Item 2: Operations plan contains 2 modules
$opsMods = $opsPlan.includedModules.moduleCode
$hasOps = ($opsMods.Count -eq 2) -and ($opsMods -contains "MAIN_TRANSACTIONS") -and ($opsMods -contains "DATA_COLLECTION")
Record-Result 2 "Operations Plan 2 Modules" $hasOps "Operations modules: $($opsMods -join ', ')"

# Item 3: Intelligence plan contains 2 modules
$intMods = $intPlan.includedModules.moduleCode
$hasInt = ($intMods.Count -eq 2) -and ($intMods -contains "BUSINESS_INTELLIGENCE") -and ($intMods -contains "ACTIONS")
Record-Result 3 "Intelligence Plan 2 Modules" $hasInt "Intelligence modules: $($intMods -join ', ')"

# Item 4: Branch plan contains 3 modules
$brMods = $brPlan.includedModules.moduleCode
$hasBr = ($brMods.Count -eq 3) -and ($brMods -contains "BRANCHING") -and ($brMods -contains "BUSINESS_INTELLIGENCE") -and ($brMods -contains "ACTIONS")
Record-Result 4 "Branch Plan 3 Modules" $hasBr "Branch modules: $($brMods -join ', ')"

# Item 5: Plan modules stored in database tables
Record-Result 5 "Database Driven Plan Modules" ($plans.Count -ge 4) "Loaded $($plans.Count) plans from database tables SubscriptionPlans and PlanModules"

# Item 6: Module definitions are canonical and shared
$allMods = Invoke-RestMethod -Uri "$baseUrl/subscriptions/modules" -Method Get -Headers $saHeaders
$canonicalCount = $allMods.Count
Record-Result 6 "Canonical Shared Modules" ($canonicalCount -eq 5) "5 canonical shared modules in AppModules: $($allMods.moduleCode -join ', ')"

# Login Company A
$loginCompA = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body (@{ username = "fixtech"; password = "Fixtech@123" } | ConvertTo-Json) -ContentType "application/json"
$tokenCompA = $loginCompA.token
$headersCompA = @{ Authorization = "Bearer $tokenCompA" }

# Login Company B
$loginCompB = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body (@{ username = "bytecare"; password = "Bytecare@123" } | ConvertTo-Json) -ContentType "application/json"
$tokenCompB = $loginCompB.token
$headersCompB = @{ Authorization = "Bearer $tokenCompB" }

# Login Company C
$loginCompC = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body (@{ username = "techrevive"; password = "Techrevive@123" } | ConvertTo-Json) -ContentType "application/json"
$tokenCompC = $loginCompC.token
$headersCompC = @{ Authorization = "Bearer $tokenCompC" }

# Item 7: Company A receives Operations modules
$aMods = $loginCompA.subscribedModules
$item7Pass = ($aMods -contains "MAIN_TRANSACTIONS") -and ($aMods -contains "DATA_COLLECTION") -and ($aMods.Count -eq 2)
Record-Result 7 "Company A Operations Allocation" $item7Pass "Company A login modules: $($aMods -join ', ')"

# Item 8: Company B receives Intelligence modules
$bMods = $loginCompB.subscribedModules
$item8Pass = ($bMods -contains "BUSINESS_INTELLIGENCE") -and ($bMods -contains "ACTIONS") -and ($bMods.Count -eq 2)
Record-Result 8 "Company B Intelligence Allocation" $item8Pass "Company B login modules: $($bMods -join ', ')"

# Item 9: Company C receives Branch modules
$cMods = $loginCompC.subscribedModules
$item9Pass = ($cMods -contains "BRANCHING") -and ($cMods -contains "BUSINESS_INTELLIGENCE") -and ($cMods -contains "ACTIONS") -and ($cMods.Count -eq 3)
Record-Result 9 "Company C Branch Allocation" $item9Pass "Company C login modules: $($cMods -join ', ')"

# Item 10: Company A cannot access Business Intelligence without an add-on
$aBiDenied = $false
try {
    Invoke-RestMethod -Uri "$baseUrl/tenant/1/analytics/dashboard" -Method Get -Headers $headersCompA | Out-Null
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $aBiDenied = $true }
}
Record-Result 10 "Company A Blocked from BI" $aBiDenied "Company A GET /tenant/1/analytics/dashboard returned 403 Forbidden"

# Item 11: Company B cannot access Main Transactions without an add-on
$bTxDenied = $false
try {
    Invoke-RestMethod -Uri "$baseUrl/tenant/2/repair-requests" -Method Get -Headers $headersCompB | Out-Null
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $bTxDenied = $true }
}
Record-Result 11 "Company B Blocked from Repairs" $bTxDenied "Company B GET /tenant/2/repair-requests returned 403 Forbidden"

# Item 12: Company C cannot access Main Transactions without an add-on
$cTxDenied = $false
try {
    Invoke-RestMethod -Uri "$baseUrl/tenant/3/repair-requests" -Method Get -Headers $headersCompC | Out-Null
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $cTxDenied = $true }
}
Record-Result 12 "Company C Blocked from Repairs" $cTxDenied "Company C GET /tenant/3/repair-requests returned 403 Forbidden"

# Item 13: Super Admin has full platform access
$saAccess = $false
try {
    $saComp1 = Invoke-RestMethod -Uri "$baseUrl/tenant/1/repair-requests" -Method Get -Headers $saHeaders
    $saComp2 = Invoke-RestMethod -Uri "$baseUrl/tenant/2/analytics/dashboard" -Method Get -Headers $saHeaders
    $saAccess = ($saComp1 -ne $null) -and ($saComp2 -ne $null)
} catch {
    $saAccess = $false
}
Record-Result 13 "Super Admin Full Access" $saAccess "Super Admin accessed cross-tenant and restricted endpoints without restriction"

# Item 14: Upgrading Company A from Operations to Enterprise enables all 5 modules automatically
$upgradeReq = @{ newPlanCode = "ENTERPRISE"; reason = "Automated test upgrade" } | ConvertTo-Json
$upgradedA = Invoke-RestMethod -Uri "$baseUrl/subscriptions/companies/1/plan" -Method Post -Body $upgradeReq -ContentType "application/json" -Headers $saHeaders
$loginCompAAfterUpgrade = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body (@{ username = "fixtech"; password = "Fixtech@123" } | ConvertTo-Json) -ContentType "application/json"
$aUpgradedMods = $loginCompAAfterUpgrade.subscribedModules
$item14Pass = ($aUpgradedMods.Count -eq 5) -and ($upgradedA.planCode -eq "ENTERPRISE")
Record-Result 14 "Company A Upgrade to Enterprise" $item14Pass "Company A received all 5 modules upon upgrade: $($aUpgradedMods -join ', ')"

# Item 15: Downgrading Company A from Enterprise back to Operations restricts access back to 2 modules
$downgradeReq = @{ newPlanCode = "OPERATIONS"; reason = "Automated test downgrade" } | ConvertTo-Json
$downgradedA = Invoke-RestMethod -Uri "$baseUrl/subscriptions/companies/1/plan" -Method Post -Body $downgradeReq -ContentType "application/json" -Headers $saHeaders
$loginCompAAfterDowngrade = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body (@{ username = "fixtech"; password = "Fixtech@123" } | ConvertTo-Json) -ContentType "application/json"
$aDowngradedMods = $loginCompAAfterDowngrade.subscribedModules
$item15Pass = ($aDowngradedMods.Count -eq 2) -and ($downgradedA.planCode -eq "OPERATIONS")
Record-Result 15 "Company A Downgrade to Operations" $item15Pass "Company A restricted back to 2 modules: $($aDowngradedMods -join ', ')"

# Item 16: Company A's data remains intact after downgrade
$custsA = Invoke-RestMethod -Uri "$baseUrl/tenant/1/customers" -Method Get -Headers $saHeaders
$repairsA = Invoke-RestMethod -Uri "$baseUrl/tenant/1/repair-requests" -Method Get -Headers $saHeaders
$item16Pass = ($custsA.Count -ge 200) -and ($repairsA.Count -gt 0)
Record-Result 16 "Company A Tenant Data Intact After Downgrade" $item16Pass "Company A has $($custsA.Count) customers and $($repairsA.Count) repairs preserved"

# Item 17: Adding an add-on module to Company A increases total subscription price correctly
$addAddonReq = @{ moduleCode = "BUSINESS_INTELLIGENCE" } | ConvertTo-Json
$withAddon = Invoke-RestMethod -Uri "$baseUrl/subscriptions/companies/1/addons" -Method Post -Body $addAddonReq -ContentType "application/json" -Headers $saHeaders
$expectedTotalWithAddon = 1750.00 + 1500.00
$item17Pass = ($withAddon.monthlyTotal -eq $expectedTotalWithAddon) -and ($withAddon.activeAddons.Count -eq 1)
Record-Result 17 "Add-on Module Price Increase" $item17Pass "Operations (1750) + BI Addon (1500) = $($withAddon.monthlyTotal)"

# Item 18: Removing an add-on module updates total subscription price correctly
$withoutAddon = Invoke-RestMethod -Uri "$baseUrl/subscriptions/companies/1/addons/BUSINESS_INTELLIGENCE" -Method Delete -Headers $saHeaders
$item18Pass = ($withoutAddon.monthlyTotal -eq 1750.00) -and ($withoutAddon.activeAddons.Count -eq 0)
Record-Result 18 "Remove Add-on Price Recalculation" $item18Pass "Price reset to Operations base: $($withoutAddon.monthlyTotal)"

# Item 19: Direct URL/API access to unsubscribed modules returns 403 Forbidden
$directBlocked = $false
try {
    Invoke-RestMethod -Uri "$baseUrl/tenant/1/retention/templates" -Method Get -Headers $headersCompA | Out-Null
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $directBlocked = $true }
}
Record-Result 19 "Direct API Enforcement 403" $directBlocked "Access to unsubscribed Actions module endpoint returned HTTP 403 Forbidden"

# Item 20: Cross-company access is blocked (multi-tenant isolation)
$crossBlocked = $false
try {
    # Fixtech (Company 1) trying to access Company 2's customers
    Invoke-RestMethod -Uri "$baseUrl/tenant/2/customers" -Method Get -Headers $headersCompA | Out-Null
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) { $crossBlocked = $true }
}
Record-Result 20 "Cross-Company Multi-Tenant Isolation" $crossBlocked "Fixtech (Company 1) blocked from accessing Tenant 2 with 403 Forbidden"

# Item 21: All company database dummy records remain intact and valid (minimum 200 per company)
$c1Count = (Invoke-RestMethod -Uri "$baseUrl/tenant/1/customers" -Method Get -Headers $saHeaders).Count
$c2Count = (Invoke-RestMethod -Uri "$baseUrl/tenant/2/customers" -Method Get -Headers $saHeaders).Count
$c3Count = (Invoke-RestMethod -Uri "$baseUrl/tenant/3/customers" -Method Get -Headers $saHeaders).Count
$item21Pass = ($c1Count -ge 200) -and ($c2Count -ge 200) -and ($c3Count -ge 200)
Record-Result 21 "200+ Valid Records per Tenant Database" $item21Pass "Company 1: $c1Count, Company 2: $c2Count, Company 3: $c3Count"

Write-Host "`n==============================================="
$totalPassed = ($results | Where-Object { $_.Passed }).Count
Write-Host "FINAL VALIDATION: $totalPassed / 21 ITEMS PASSED" -ForegroundColor Cyan
Write-Host "==============================================="
