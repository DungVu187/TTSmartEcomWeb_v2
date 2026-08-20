[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS',
    [ValidateSet('TTSmart_Control_V1_Test', 'TTSmart_Operational_V1_Test')]
    [string] $DatabaseName
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd
if ([string]::IsNullOrWhiteSpace($DatabaseName)) { throw 'Phai chi ro dung ten database test duoc cap phep.' }

$sql = @'
SET NOCOUNT ON;
DECLARE @schema nvarchar(max);
;WITH SchemaRows AS
(
    SELECT CONCAT(N'T|', s.name, N'|', t.name) AS Item
    FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id=t.schema_id
    WHERE t.is_ms_shipped=0
    UNION ALL
    SELECT CONCAT(N'C|', OBJECT_SCHEMA_NAME(c.object_id), N'|', OBJECT_NAME(c.object_id), N'|', c.column_id, N'|', c.name, N'|', ty.name, N'|', c.max_length, N'|', c.precision, N'|', c.scale, N'|', c.is_nullable, N'|', c.is_identity, N'|', c.is_computed, N'|', ISNULL(c.collation_name,N''), N'|', ISNULL(dc.name,N''), N'|', ISNULL(dc.definition,N''), N'|', ISNULL(cc.definition,N''))
    FROM sys.columns AS c
    JOIN sys.types AS ty ON ty.user_type_id=c.user_type_id
    LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
    LEFT JOIN sys.computed_columns AS cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
    WHERE OBJECTPROPERTY(c.object_id,N'IsUserTable')=1
    UNION ALL
    SELECT CONCAT(N'KC|', OBJECT_SCHEMA_NAME(kc.parent_object_id), N'|', OBJECT_NAME(kc.parent_object_id), N'|', kc.name, N'|', kc.type, N'|', kc.is_system_named, N'|', ic.key_ordinal, N'|', COL_NAME(ic.object_id,ic.column_id), N'|', ic.is_descending_key)
    FROM sys.key_constraints AS kc
    JOIN sys.index_columns AS ic ON ic.object_id=kc.parent_object_id AND ic.index_id=kc.unique_index_id AND ic.key_ordinal>0
    UNION ALL
    SELECT CONCAT(N'I|', OBJECT_SCHEMA_NAME(i.object_id), N'|', OBJECT_NAME(i.object_id), N'|', i.name, N'|', i.type_desc, N'|', i.is_unique, N'|', i.is_primary_key, N'|', i.is_unique_constraint, N'|', i.is_disabled, N'|', i.has_filter, N'|', ISNULL(i.filter_definition,N''))
    FROM sys.indexes AS i WHERE OBJECTPROPERTY(i.object_id,N'IsUserTable')=1 AND i.is_hypothetical=0 AND i.index_id>0
    UNION ALL
    SELECT CONCAT(N'IC|', OBJECT_SCHEMA_NAME(ic.object_id), N'|', OBJECT_NAME(ic.object_id), N'|', i.name, N'|', ic.index_column_id, N'|', COL_NAME(ic.object_id,ic.column_id), N'|', ic.key_ordinal, N'|', ic.is_descending_key, N'|', ic.is_included_column)
    FROM sys.index_columns AS ic JOIN sys.indexes AS i ON i.object_id=ic.object_id AND i.index_id=ic.index_id
    WHERE OBJECTPROPERTY(ic.object_id,N'IsUserTable')=1 AND i.is_hypothetical=0 AND i.index_id>0
    UNION ALL
    SELECT CONCAT(N'CK|', OBJECT_SCHEMA_NAME(parent_object_id), N'|', OBJECT_NAME(parent_object_id), N'|', name, N'|', is_system_named, N'|', is_disabled, N'|', is_not_trusted, N'|', definition)
    FROM sys.check_constraints
    UNION ALL
    SELECT CONCAT(N'FK|', OBJECT_SCHEMA_NAME(fk.parent_object_id), N'|', OBJECT_NAME(fk.parent_object_id), N'|', fk.name, N'|', fk.is_system_named, N'|', OBJECT_SCHEMA_NAME(fk.referenced_object_id), N'|', OBJECT_NAME(fk.referenced_object_id), N'|', fk.is_disabled, N'|', fk.is_not_trusted, N'|', fk.delete_referential_action_desc, N'|', fk.update_referential_action_desc, N'|', fkc.constraint_column_id, N'|', COL_NAME(fkc.parent_object_id,fkc.parent_column_id), N'|', COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id))
    FROM sys.foreign_keys AS fk JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id=fk.object_id
    UNION ALL
    SELECT CONCAT(N'TR|', SCHEMA_NAME(t.schema_id), N'|', t.name, N'|', tr.name, N'|', tr.is_disabled, N'|', sm.uses_ansi_nulls, N'|', sm.uses_quoted_identifier, N'|', sm.is_schema_bound, N'|', ISNULL(USER_NAME(sm.execute_as_principal_id),N''), N'|', ISNULL(sm.definition,N''))
    FROM sys.triggers AS tr
    JOIN sys.tables AS t ON t.object_id=tr.parent_id
    LEFT JOIN sys.sql_modules AS sm ON sm.object_id=tr.object_id
    WHERE tr.is_ms_shipped=0
    UNION ALL
    SELECT CONCAT(N'M|', SCHEMA_NAME(o.schema_id), N'|', o.type, N'|', o.name, N'|', sm.uses_ansi_nulls, N'|', sm.uses_quoted_identifier, N'|', sm.is_schema_bound, N'|', ISNULL(USER_NAME(sm.execute_as_principal_id),N''), N'|', ISNULL(sm.definition,N''))
    FROM sys.objects AS o JOIN sys.sql_modules AS sm ON sm.object_id=o.object_id
    WHERE o.is_ms_shipped=0 AND o.type IN(N'P',N'V',N'FN',N'IF',N'TF')
    UNION ALL
    SELECT CONCAT(N'PR|', p.name, N'|', p.type_desc, N'|', ISNULL(USER_NAME(p.owning_principal_id),N''), N'|', p.authentication_type_desc)
    FROM sys.database_principals AS p
    WHERE p.principal_id>4 AND p.name NOT LIKE N'##MS[_]%' AND (p.type=N'R' AND p.is_fixed_role=0 OR p.type IN(N'S',N'U',N'E',N'X'))
    UNION ALL
    SELECT CONCAT(N'RM|', r.name, N'|', m.name)
    FROM sys.database_role_members AS rm
    JOIN sys.database_principals AS r ON r.principal_id=rm.role_principal_id
    JOIN sys.database_principals AS m ON m.principal_id=rm.member_principal_id
    WHERE r.principal_id>4 AND r.is_fixed_role=0 AND r.name NOT LIKE N'##MS[_]%'
    UNION ALL
    SELECT CONCAT(N'PM|', grantee.name, N'|', grantor.name, N'|', permission.class_desc, N'|', permission.permission_name, N'|', permission.state_desc, N'|', CASE permission.class WHEN 0 THEN DB_NAME() WHEN 1 THEN CONCAT(OBJECT_SCHEMA_NAME(permission.major_id),N'.',OBJECT_NAME(permission.major_id)) WHEN 3 THEN SCHEMA_NAME(permission.major_id) ELSE CONVERT(nvarchar(30),permission.major_id) END, N'|', permission.minor_id)
    FROM sys.database_permissions AS permission
    JOIN sys.database_principals AS grantee ON grantee.principal_id=permission.grantee_principal_id
    JOIN sys.database_principals AS grantor ON grantor.principal_id=permission.grantor_principal_id
    WHERE grantee.principal_id>4 AND grantee.name NOT LIKE N'##MS[_]%'
    UNION ALL
    SELECT CONCAT(N'QSO|', actual_state_desc, N'|', desired_state_desc, N'|', query_capture_mode_desc, N'|', size_based_cleanup_mode_desc, N'|', interval_length_minutes, N'|', stale_query_threshold_days, N'|', max_storage_size_mb, N'|', flush_interval_seconds, N'|', wait_stats_capture_mode_desc)
    FROM sys.database_query_store_options
    UNION ALL
    SELECT CONCAT(N'DSC|', name, N'|', CONVERT(nvarchar(128),value), N'|', CONVERT(nvarchar(128),value_for_secondary))
    FROM sys.database_scoped_configurations
    UNION ALL
    SELECT CONCAT(N'DB|', collation_name, N'|', compatibility_level, N'|', recovery_model_desc, N'|', page_verify_option_desc, N'|', is_auto_close_on, N'|', is_auto_shrink_on, N'|', is_read_committed_snapshot_on, N'|', snapshot_isolation_state_desc, N'|', is_ansi_null_default_on, N'|', is_ansi_nulls_on, N'|', is_ansi_padding_on, N'|', is_ansi_warnings_on, N'|', is_arithabort_on, N'|', is_concat_null_yields_null_on, N'|', is_quoted_identifier_on, N'|', is_numeric_roundabort_on)
    FROM sys.databases WHERE database_id=DB_ID()
), CanonicalRows AS
(
    SELECT Item COLLATE DATABASE_DEFAULT AS Item FROM SchemaRows
)
SELECT @schema=(SELECT Item FROM CanonicalRows ORDER BY Item FOR XML PATH(N''),TYPE).value(N'.',N'nvarchar(max)');
SELECT CONVERT(varchar(64), HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), @schema)), 2) AS SchemaFingerprint;
'@

$value = & $sqlCmd -S $ServerInstance -E -d $DatabaseName -b -I -h -1 -W -Q $sql
if ($LASTEXITCODE -ne 0) { throw "Khong the tao dau van tay schema cho database test: $(($value | Out-String).Trim())" }
$fingerprint = ($value | Out-String).Trim()
if ($fingerprint -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Dau van tay schema khong hop le.' }
$fingerprint.ToUpperInvariant()
