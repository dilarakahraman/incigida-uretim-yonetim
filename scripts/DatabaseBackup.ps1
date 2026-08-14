param(
    [string]$SqlInstance = ".\SQLEXPRESS1",
    [string]$Database = "SusamUretim",
    [string]$LocalBackupDirectory = "C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS1\MSSQL\Backup\SusamUretim",
    [string]$SecondaryBackupDirectory = "D:\SusamUretim-Yedekleri",
    [int]$LocalRetentionDays = 7,
    [int]$SecondaryRetentionDays = 30
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$fileName = "$Database-$timestamp.bak"
$localFile = Join-Path $LocalBackupDirectory $fileName
$secondaryFile = Join-Path $SecondaryBackupDirectory $fileName

New-Item -ItemType Directory -Path $SecondaryBackupDirectory -Force | Out-Null

$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = "Server=$SqlInstance;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;"
$connection.Open()
try {
    $escapedDatabase = $Database.Replace("]", "]]" )
    $escapedLocalDirectory = $LocalBackupDirectory.Replace("'", "''")
    $escapedLocalPath = $localFile.Replace("'", "''")
    $escapedSecondaryPath = $secondaryFile.Replace("'", "''")
    $localCutoff = (Get-Date).AddDays(-$LocalRetentionDays).ToString("yyyy-MM-ddTHH:mm:ss")
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 600
    $command.CommandText = @"
EXEC master.dbo.xp_create_subdir N'$escapedLocalDirectory';
BACKUP DATABASE [$escapedDatabase] TO DISK=N'$escapedLocalPath' WITH COPY_ONLY, INIT, CHECKSUM;
RESTORE VERIFYONLY FROM DISK=N'$escapedLocalPath' WITH CHECKSUM;
BACKUP DATABASE [$escapedDatabase] TO DISK=N'$escapedSecondaryPath' WITH COPY_ONLY, INIT, CHECKSUM;
RESTORE VERIFYONLY FROM DISK=N'$escapedSecondaryPath' WITH CHECKSUM;
EXEC master.dbo.xp_delete_file 0,N'$escapedLocalDirectory',N'bak',N'$localCutoff',0;
"@
    [void]$command.ExecuteNonQuery()
}
finally {
    $connection.Dispose()
}

$secondaryHash = (Get-FileHash -LiteralPath $secondaryFile -Algorithm SHA256).Hash

$now = Get-Date
Get-ChildItem -LiteralPath $SecondaryBackupDirectory -Filter "$Database-*.bak" -File |
    Where-Object LastWriteTime -lt $now.AddDays(-$SecondaryRetentionDays) |
    Remove-Item -Force

$logFile = Join-Path $SecondaryBackupDirectory "backup-log.txt"
"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') OK $fileName SHA256=$secondaryHash" | Add-Content -LiteralPath $logFile -Encoding UTF8
Write-Output "BACKUP_OK|$localFile|$secondaryFile|$secondaryHash"
