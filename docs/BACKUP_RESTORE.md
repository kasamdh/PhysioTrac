# SQL Server backup, restore, and point-in-time recovery

PhysioTrac stores all tenant data (PHI included) in a single SQL Server
database (`PhysioTrac`). This document covers backing it up, restoring it,
and recovering to a point in time, for both the containerized SQL Server
(`docker-compose.yml`'s `sqlserver` service) and a managed cloud database
(Azure SQL Database / Amazon RDS for SQL Server).

For patient data specifically, backups are also a HIPAA Security Rule
requirement (the contingency-plan/data-backup-plan standard) -- see
[HIPAA_CHECKLIST.md](HIPAA_CHECKLIST.md).

## Recovery model

Point-in-time recovery requires the `FULL` recovery model (the default for
new SQL Server databases, including this app's). Under `SIMPLE`, the
transaction log is truncated on checkpoint and you can only restore to the
moment of your last full/differential backup, not to an arbitrary point
between backups. Confirm the model in use:

```sql
SELECT name, recovery_model_desc FROM sys.databases WHERE name = 'PhysioTrac';
```

## Self-hosted / containerized SQL Server

The `sqlserver` container persists data in the `sqlserver-data` named
volume, but a volume is not a backup -- it shares the same underlying disk
as the container host and offers no protection against disk failure,
accidental `docker volume rm`, or a bad migration. Take real
`.bak`/`.trn` backups on a schedule, and copy them off the host (to blob
storage, another host, etc.).

### Full backup

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -Q \
  "BACKUP DATABASE PhysioTrac TO DISK = N'/var/opt/mssql/backup/PhysioTrac_full.bak' WITH INIT, COMPRESSION, STATS = 10;"
```

(Create `/var/opt/mssql/backup` inside the container -- or better, mount it
as its own volume/bind mount -- so backup files survive independently of
the data volume.)

### Transaction log backups (required for point-in-time recovery)

Schedule these frequently (e.g. every 15 minutes) once a full backup
exists, under the `FULL` recovery model:

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -Q \
  "BACKUP LOG PhysioTrac TO DISK = N'/var/opt/mssql/backup/PhysioTrac_$(date +%Y%m%d_%H%M%S).trn' WITH COMPRESSION, STATS = 10;"
```

A weekly differential backup (`BACKUP DATABASE PhysioTrac TO DISK = ... WITH DIFFERENTIAL`)
keeps the restore chain (full + differential + logs since the differential)
shorter than replaying every log since the last full backup.

### Restore (to latest)

```sql
RESTORE DATABASE PhysioTrac FROM DISK = N'/var/opt/mssql/backup/PhysioTrac_full.bak' WITH NORECOVERY, REPLACE;
RESTORE LOG PhysioTrac FROM DISK = N'/var/opt/mssql/backup/PhysioTrac_20260101_120000.trn' WITH NORECOVERY;
-- repeat RESTORE LOG for every subsequent .trn file, in order, then finish with:
RESTORE DATABASE PhysioTrac WITH RECOVERY;
```

### Point-in-time recovery

Same as above, but stop at the target moment instead of restoring every
log file to its end:

```sql
RESTORE LOG PhysioTrac FROM DISK = N'/var/opt/mssql/backup/PhysioTrac_20260101_120000.trn'
  WITH NORECOVERY, STOPAT = '2026-01-01T12:07:00';
RESTORE DATABASE PhysioTrac WITH RECOVERY;
```

`STOPAT` must fall within the time range covered by the log backup you
apply it to -- if the target moment is in an earlier `.trn` file, apply
`STOPAT` on that file instead and don't restore any files after it.

### Verifying restores

Restores are only real if they're tested. Periodically restore the latest
backup chain into a scratch database (`RESTORE DATABASE PhysioTrac_verify FROM DISK = ... WITH MOVE ...`)
on a non-production instance and confirm the app can connect to it and the
row counts/latest timestamps look sane. An untested backup is not a backup.

## Managed database services (Azure SQL Database / Amazon RDS)

If you follow [DEPLOYMENT_AZURE.md](DEPLOYMENT_AZURE.md) or
[DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) and run against a managed database
instead of the `sqlserver` container, backups and point-in-time recovery
are handled by the platform -- you configure retention, not backup jobs:

- **Azure SQL Database**: automated backups (full/differential/log) are
  always on. Point-in-time restore is available for any point within the
  configured retention window (default 7 days on most tiers, configurable
  up to 35 days). Restore via the Azure Portal, CLI (`az sql db restore`),
  or ARM/Bicep -- it creates a *new* database at the restored point; you
  then repoint `ConnectionStrings__Default` at it (or rename databases) to
  cut over.
- **Amazon RDS for SQL Server**: enable automated backups (retention
  1-35 days) and note the daily backup window. Point-in-time restore
  (`aws rds restore-db-instance-to-point-in-time`) also creates a new DB
  instance at the target timestamp; cut over the same way. RDS also
  supports on-demand manual snapshots for longer retention than the
  automated-backup window allows.

Either way, the same operational discipline applies: define a retention
window long enough for your compliance/business needs, and periodically
*prove* a restore works rather than assuming the platform's backups are
sufficient on faith.
