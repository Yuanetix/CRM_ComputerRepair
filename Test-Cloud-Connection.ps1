# Test script to verify all 4 MonsterASP cloud databases reachability
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host " Testing MonsterASP Cloud Databases Reachability..." -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

$databases = [ordered]@{
    "MasterCRM (db70848)"   = @{ Server = "5.9.179.199,1433"; Db = "db70848"; User = "db70848"; Pass = "L!e48E=no+6Y" }
    "Fixtech (db70863)"     = @{ Server = "5.9.179.199,1433"; Db = "db70863"; User = "db70863"; Pass = "8a@H-Pi2Xw9?" }
    "Bytecare (db70865)"    = @{ Server = "5.9.179.199,1433"; Db = "db70865"; User = "db70865"; Pass = "3t?YB+2n#7sE" }
    "Techrevive (db70866)"  = @{ Server = "5.9.179.199,1433"; Db = "db70866"; User = "db70866"; Pass = "2Dy!W+4z?mK5" }
}

$allOk = $true
foreach ($name in $databases.Keys) {
    $info = $databases[$name]
    $cs = "Server=$($info.Server); Database=$($info.Db); User Id=$($info.User); Password=$($info.Pass); Encrypt=False; TrustServerCertificate=True; MultipleActiveResultSets=True; Connect Timeout=6;"
    
    Write-Host "`nConnecting to $name [$($info.Server) / $($info.Db)]..." -ForegroundColor Yellow -NoNewline
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
        $conn.Open()
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sys.tables"
        $tableCount = $cmd.ExecuteScalar()

        $cmd.CommandText = "SELECT ISNULL(SUM(p.rows), 0) FROM sys.tables t JOIN sys.partitions p ON t.object_id = p.object_id WHERE p.index_id IN (0,1)"
        $rowCount = $cmd.ExecuteScalar()

        Write-Host " [SUCCESS]" -ForegroundColor Green
        Write-Host "   Tables: $tableCount | Total Rows: $rowCount" -ForegroundColor DarkCyan
        $conn.Close()
    }
    catch {
        $allOk = $false
        Write-Host " [FAILED]" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n===========================================================" -ForegroundColor Cyan
if ($allOk) {
    Write-Host " ALL MONSTERASP DATABASES ARE ONLINE AND FULLY OPERATIONAL!" -ForegroundColor Green
} else {
    Write-Host " SOME DATABASES COULD NOT BE REACHED. Check network connection." -ForegroundColor Yellow
}
Write-Host "===========================================================" -ForegroundColor Cyan
