# Script to migrate local database records to MonsterASP cloud database
$localTenantCs = "Server=(localdb)\MSSQLLocalDB;Database=DB_TenantRepairs_Company1;Integrated Security=True;TrustServerCertificate=True;"
$localMasterCs = "Server=(localdb)\MSSQLLocalDB;Database=DB_MasterCRM;Integrated Security=True;TrustServerCertificate=True;"
$cloudCs = "Server=db70848.public.databaseasp.net,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"

function Sync-Table {
    param(
        [string]$srcCs,
        [string]$srcTable,
        [string]$dstTable = $srcTable
    )
    Write-Host "`n>>> Checking $srcTable -> $dstTable..." -ForegroundColor Cyan
    $srcConn = New-Object System.Data.SqlClient.SqlConnection($srcCs)
    $dstConn = New-Object System.Data.SqlClient.SqlConnection($cloudCs)
    $srcConn.Open()
    $dstConn.Open()

    try {
        # Check source count
        $cmdCnt = $srcConn.CreateCommand()
        $cmdCnt.CommandText = "SELECT COUNT(*) FROM [$srcTable]"
        $srcCount = [int]$cmdCnt.ExecuteScalar()
        if ($srcCount -eq 0) {
            Write-Host "  0 rows in source $srcTable. Skipping." -ForegroundColor Gray
            return
        }

        # Destination count
        $dstCntCmd = $dstConn.CreateCommand()
        $dstCntCmd.CommandText = "SELECT COUNT(*) FROM [$dstTable]"
        $dstCount = [int]$dstCntCmd.ExecuteScalar()
        if ($dstCount -gt 0) {
            Write-Host "  Destination already has $dstCount rows in $dstTable." -ForegroundColor Yellow
            return
        }

        # Get matching columns
        $cmdColsDst = $dstConn.CreateCommand()
        $cmdColsDst.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$dstTable'"
        $rdrDst = $cmdColsDst.ExecuteReader()
        $dstCols = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        while ($rdrDst.Read()) { [void]$dstCols.Add($rdrDst.GetString(0)) }
        $rdrDst.Close()

        $cmdColsSrc = $srcConn.CreateCommand()
        $cmdColsSrc.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$srcTable'"
        $rdrSrc = $cmdColsSrc.ExecuteReader()
        $matchingCols = [System.Collections.Generic.List[string]]::new()
        while ($rdrSrc.Read()) {
            $col = $rdrSrc.GetString(0)
            if ($dstCols.Contains($col)) {
                $matchingCols.Add($col)
            }
        }
        $rdrSrc.Close()

        if ($matchingCols.Count -eq 0) {
            Write-Host "  No matching columns between $srcTable and $dstTable!" -ForegroundColor Red
            return
        }

        $colListArray = @()
        foreach ($c in $matchingCols) {
            $colListArray += "[$c]"
        }
        $colSelectStr = $colListArray -join ", "

        Write-Host "  Found $($matchingCols.Count) matching columns. Transferring $srcCount rows..." -ForegroundColor White

        $selCmd = $srcConn.CreateCommand()
        $selCmd.CommandText = "SELECT $colSelectStr FROM [$srcTable]"
        $srcReader = $selCmd.ExecuteReader()

        $copyOptions = [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity -bor [System.Data.SqlClient.SqlBulkCopyOptions]::KeepNulls
        $bcp = New-Object System.Data.SqlClient.SqlBulkCopy($dstConn, $copyOptions, $null)
        $bcp.DestinationTableName = "[$dstTable]"
        $bcp.BulkCopyTimeout = 300
        $bcp.BatchSize = 1000

        foreach ($col in $matchingCols) {
            [void]$bcp.ColumnMappings.Add($col, $col)
        }

        $bcp.WriteToServer($srcReader)
        $srcReader.Close()

        # Check new destination count
        $newDstCnt = $dstCntCmd.ExecuteScalar()
        Write-Host "  SUCCESS: Migrated $newDstCnt rows into $dstTable on MonsterASP!" -ForegroundColor Green
    }
    catch {
        $err = $_.Exception.Message
        Write-Host "  ERROR migrating $srcTable - $err" -ForegroundColor Red
        if ($_.Exception.InnerException) {
            $inner = $_.Exception.InnerException.Message
            Write-Host "  Details: $inner" -ForegroundColor Red
        }
    }
    finally {
        $srcConn.Close()
        $dstConn.Close()
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " STARTING DATA SYNC TO MONSTERASP (db70848)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Master tables
Sync-Table $localMasterCs "AspNetRoles"
Sync-Table $localMasterCs "AspNetUsers"
Sync-Table $localMasterCs "AspNetUserRoles"
Sync-Table $localMasterCs "Companies"
Sync-Table $localMasterCs "TermsAndConditionsSet"

# 2. Tenant tables in FK dependency order
Sync-Table $localTenantCs "Suppliers"
Sync-Table $localTenantCs "Parts"
Sync-Table $localTenantCs "Customers"
Sync-Table $localTenantCs "Devices"
Sync-Table $localTenantCs "RepairRequests"
Sync-Table $localTenantCs "RepairParts"
Sync-Table $localTenantCs "RepairStatusHistories"
Sync-Table $localTenantCs "Payments"
Sync-Table $localTenantCs "CustomerInteractions"
Sync-Table $localTenantCs "FollowUps"
Sync-Table $localTenantCs "RetentionSettings"
Sync-Table $localTenantCs "RetentionEmailTemplates"
Sync-Table $localTenantCs "RetentionRequests"
Sync-Table $localTenantCs "RetentionEmailLogs"

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " SYNC COMPLETED!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
