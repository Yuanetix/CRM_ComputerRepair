# Script to populate all 4 MonsterASP Cloud Databases from local databases
$ErrorActionPreference = "Stop"

$localMasterCs = "Server=(localdb)\MSSQLLocalDB;Database=DB_MasterCRM;Integrated Security=True;TrustServerCertificate=True;"
$localTenant1Cs = "Server=(localdb)\MSSQLLocalDB;Database=DB_TenantRepairs_Company1;Integrated Security=True;TrustServerCertificate=True;"
$localTenant2Cs = "Server=(localdb)\MSSQLLocalDB;Database=DB_TenantRepairs_Company2;Integrated Security=True;TrustServerCertificate=True;"
$localTenant3Cs = "Server=(localdb)\MSSQLLocalDB;Database=DB_TenantRepairs_Company3;Integrated Security=True;TrustServerCertificate=True;"

$cloudMasterCs   = "Server=5.9.179.199,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$cloudFixtechCs  = "Server=5.9.179.199,1433;Database=db70863;User Id=db70863;Password=8a@H-Pi2Xw9?;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$cloudBytecareCs = "Server=5.9.179.199,1433;Database=db70865;User Id=db70865;Password=3t?YB+2n#7sE;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$cloudTechrevCs  = "Server=5.9.179.199,1433;Database=db70866;User Id=db70866;Password=2Dy!W+4z?mK5;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"

function Copy-TableData {
    param(
        [string]$srcCs,
        [string]$dstCs,
        [string]$tableName,
        [bool]$clearDestination = $false
    )

    $srcConn = New-Object System.Data.SqlClient.SqlConnection($srcCs)
    $dstConn = New-Object System.Data.SqlClient.SqlConnection($dstCs)
    $srcConn.Open()
    $dstConn.Open()

    try {
        # Check source count
        $cntCmd = $srcConn.CreateCommand()
        $cntCmd.CommandText = "SELECT COUNT(*) FROM [$tableName]"
        $srcCount = [int]$cntCmd.ExecuteScalar()
        if ($srcCount -eq 0) {
            Write-Host "  [$tableName]: 0 rows in source. Skipping." -ForegroundColor Gray
            return
        }

        # Check destination count
        $dstCntCmd = $dstConn.CreateCommand()
        $dstCntCmd.CommandText = "SELECT COUNT(*) FROM [$tableName]"
        $dstCount = [int]$dstCntCmd.ExecuteScalar()

        if ($dstCount -gt 0 -and -not $clearDestination) {
            Write-Host "  [$tableName]: already has $dstCount rows. Skipping." -ForegroundColor Yellow
            return
        }

        if ($clearDestination -and $dstCount -gt 0) {
            $delCmd = $dstConn.CreateCommand()
            $delCmd.CommandText = "DELETE FROM [$tableName]"
            $delCmd.ExecuteNonQuery()
            Write-Host "  [$tableName]: cleared $dstCount existing rows." -ForegroundColor DarkGray
        }

        # Match columns between source and destination
        $dstColsCmd = $dstConn.CreateCommand()
        $dstColsCmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tableName'"
        $rdr = $dstColsCmd.ExecuteReader()
        $dstColSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        while ($rdr.Read()) { [void]$dstColSet.Add($rdr.GetString(0)) }
        $rdr.Close()

        $srcColsCmd = $srcConn.CreateCommand()
        $srcColsCmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tableName'"
        $rdr = $srcColsCmd.ExecuteReader()
        $matchedCols = [System.Collections.Generic.List[string]]::new()
        while ($rdr.Read()) {
            $col = $rdr.GetString(0)
            if ($dstColSet.Contains($col)) {
                $matchedCols.Add($col)
            }
        }
        $rdr.Close()

        if ($matchedCols.Count -eq 0) {
            Write-Host "  [$tableName]: No matching columns found!" -ForegroundColor Red
            return
        }

        $colListSql = ($matchedCols | ForEach-Object { "[$_]" }) -join ", "
        $selCmd = $srcConn.CreateCommand()
        $selCmd.CommandText = "SELECT $colListSql FROM [$tableName]"
        $reader = $selCmd.ExecuteReader()

        $copyOpts = [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity -bor [System.Data.SqlClient.SqlBulkCopyOptions]::KeepNulls
        $bcp = New-Object System.Data.SqlClient.SqlBulkCopy($dstConn, $copyOpts, $null)
        $bcp.DestinationTableName = "[$tableName]"
        $bcp.BulkCopyTimeout = 300
        $bcp.BatchSize = 1000

        foreach ($c in $matchedCols) {
            [void]$bcp.ColumnMappings.Add($c, $c)
        }

        $bcp.WriteToServer($reader)
        $reader.Close()

        $newDstCount = [int]$dstCntCmd.ExecuteScalar()
        Write-Host "  [$tableName]: successfully migrated $newDstCount rows!" -ForegroundColor Green
    }
    finally {
        $srcConn.Close()
        $dstConn.Close()
    }
}

function Disable-Fks($cs) {
    $c = New-Object System.Data.SqlClient.SqlConnection($cs)
    $c.Open()
    $cmd = $c.CreateCommand()
    $cmd.CommandText = "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'"
    $cmd.ExecuteNonQuery()
    $c.Close()
}

function Enable-Fks($cs) {
    $c = New-Object System.Data.SqlClient.SqlConnection($cs)
    $c.Open()
    $cmd = $c.CreateCommand()
    $cmd.CommandText = "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all'"
    try { $cmd.ExecuteNonQuery() } catch { }
    $c.Close()
}

# ==========================================================
# 1. MIGRATE MASTER CRM DATA (db70848)
# ==========================================================
Write-Host "`n>>> 1. Migrating DB_MasterCRM -> db70848..." -ForegroundColor Cyan
Disable-Fks $cloudMasterCs

$masterTables = @(
    "AspNetRoles",
    "AspNetUsers",
    "AspNetUserRoles",
    "SubscriptionPlans",
    "Subscriptions",
    "Companies",
    "CompanyDatabases",
    "Branches",
    "BillingRecords",
    "TermsAndConditionsSet",
    "LoyaltyPrograms",
    "CustomerLoyaltyAccounts",
    "AuditLogs"
)

foreach ($t in $masterTables) {
    Copy-TableData $localMasterCs $cloudMasterCs $t $true
}

# Ensure AspNetUsers.CompanyId and Companies fields are updated in cloud Master CRM
$cM = New-Object System.Data.SqlClient.SqlConnection($cloudMasterCs)
$cM.Open()
$cmdM = $cM.CreateCommand()
# Update CompanyId for each known user if null
$cmdM.CommandText = @"
UPDATE AspNetUsers SET CompanyId = 1 WHERE UserName IN ('fixtech', 'fixtechManager', 'fixtechStaff', 'superadmin') AND (CompanyId IS NULL OR CompanyId <> 1);
UPDATE AspNetUsers SET CompanyId = 2 WHERE UserName IN ('bytecare', 'bytecareManager', 'bytecareStaff') AND (CompanyId IS NULL OR CompanyId <> 2);
UPDATE AspNetUsers SET CompanyId = 3 WHERE UserName IN ('techrevive', 'techreviveManager', 'techreviveStaff') AND (CompanyId IS NULL OR CompanyId <> 3);
"@
$cmdM.ExecuteNonQuery()

# Update or insert CompanyDatabases with the MonsterASP cloud database connections
$cmdM.CommandText = @"
IF NOT EXISTS (SELECT * FROM CompanyDatabases WHERE CompanyId = 1)
    INSERT INTO CompanyDatabases (CompanyId, ServerName, DatabaseName, CredentialKey, IsActive)
    VALUES (1, '5.9.179.199,1433', 'db70863', 'Fixtech', 1);
ELSE
    UPDATE CompanyDatabases SET ServerName = '5.9.179.199,1433', DatabaseName = 'db70863', CredentialKey = 'Fixtech', IsActive = 1 WHERE CompanyId = 1;

IF NOT EXISTS (SELECT * FROM CompanyDatabases WHERE CompanyId = 2)
    INSERT INTO CompanyDatabases (CompanyId, ServerName, DatabaseName, CredentialKey, IsActive)
    VALUES (2, '5.9.179.199,1433', 'db70865', 'Bytecare', 1);
ELSE
    UPDATE CompanyDatabases SET ServerName = '5.9.179.199,1433', DatabaseName = 'db70865', CredentialKey = 'Bytecare', IsActive = 1 WHERE CompanyId = 2;

IF NOT EXISTS (SELECT * FROM CompanyDatabases WHERE CompanyId = 3)
    INSERT INTO CompanyDatabases (CompanyId, ServerName, DatabaseName, CredentialKey, IsActive)
    VALUES (3, '5.9.179.199,1433', 'db70866', 'Techrevive', 1);
ELSE
    UPDATE CompanyDatabases SET ServerName = '5.9.179.199,1433', DatabaseName = 'db70866', CredentialKey = 'Techrevive', IsActive = 1 WHERE CompanyId = 3;
"@
$cmdM.ExecuteNonQuery()
$cM.Close()

Enable-Fks $cloudMasterCs

# ==========================================================
# 2. MIGRATE TENANT FIXTECH (Company 1 -> db70863)
# ==========================================================
Write-Host "`n>>> 2. Migrating Fixtech (Company 1) -> db70863..." -ForegroundColor Cyan
Disable-Fks $cloudFixtechCs

$tenantTables = @(
    "Suppliers",
    "Parts",
    "Customers",
    "Devices",
    "RepairRequests",
    "RepairParts",
    "RepairStatusHistories",
    "Payments",
    "CustomerInteractions",
    "FollowUps",
    "RetentionSettings",
    "RetentionEmailTemplates",
    "RetentionRequests",
    "RetentionEmailLogs"
)

foreach ($t in $tenantTables) {
    Copy-TableData $localTenant1Cs $cloudFixtechCs $t $true
}
Enable-Fks $cloudFixtechCs

# ==========================================================
# 3. MIGRATE TENANT BYTECARE (Company 2 -> db70865)
# ==========================================================
Write-Host "`n>>> 3. Migrating Bytecare (Company 2) -> db70865..." -ForegroundColor Cyan
Disable-Fks $cloudBytecareCs
foreach ($t in $tenantTables) {
    Copy-TableData $localTenant2Cs $cloudBytecareCs $t $true
}
Enable-Fks $cloudBytecareCs

# ==========================================================
# 4. MIGRATE TENANT TECHREVIVE (Company 3 -> db70866)
# ==========================================================
Write-Host "`n>>> 4. Migrating Techrevive (Company 3) -> db70866..." -ForegroundColor Cyan
Disable-Fks $cloudTechrevCs
foreach ($t in $tenantTables) {
    Copy-TableData $localTenant3Cs $cloudTechrevCs $t $true
}
Enable-Fks $cloudTechrevCs

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " ALL 4 MONSTERASP DATABASES ARE SUCCESSFULLY POPULATED!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
