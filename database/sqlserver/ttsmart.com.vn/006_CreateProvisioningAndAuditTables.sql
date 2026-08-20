/* Migration 006: provisioning database chi nhánh và nhật ký audit. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [ttsmart.com.vn];
GO
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 6)
BEGIN
    PRINT N'Migration 006 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 5)
    THROW 51006, N'Chưa áp dụng migration 005.', 1;

BEGIN TRANSACTION;

CREATE TABLE dbo.ProvisioningJobs
(
    ProvisioningJobId uniqueidentifier NOT NULL,
    BranchDatabaseId uniqueidentifier NOT NULL,
    OperationType tinyint NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_ProvisioningJobs_Status DEFAULT (0),
    IdempotencyKey nvarchar(128) NOT NULL,
    RequestedByUserId uniqueidentifier NULL,
    RequestedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProvisioningJobs_RequestedAtUtc DEFAULT SYSUTCDATETIME(),
    StartedAtUtc datetime2(7) NULL,
    FinishedAtUtc datetime2(7) NULL,
    AttemptCount int NOT NULL CONSTRAINT DF_ProvisioningJobs_AttemptCount DEFAULT (0),
    CurrentStep nvarchar(100) NULL,
    ExpectedSchemaVersion nvarchar(64) NULL,
    FailureCode nvarchar(100) NULL,
    FailureSummary nvarchar(1000) NULL,
    CorrelationId uniqueidentifier NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_ProvisioningJobs_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProvisioningJobs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProvisioningJobs_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_ProvisioningJobs PRIMARY KEY CLUSTERED (ProvisioningJobId),
    CONSTRAINT FK_ProvisioningJobs_BranchDatabases FOREIGN KEY (BranchDatabaseId) REFERENCES dbo.BranchDatabases (BranchDatabaseId),
    CONSTRAINT FK_ProvisioningJobs_RequestedByUsers FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_ProvisioningJobs_IdempotencyKey UNIQUE (IdempotencyKey),
    CONSTRAINT CK_ProvisioningJobs_OperationType CHECK (OperationType IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_ProvisioningJobs_Status CHECK (Status IN (0, 1, 2, 3, 4, 5, 6, 7)),
    CONSTRAINT CK_ProvisioningJobs_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_ProvisioningJobs_Timestamps CHECK ((StartedAtUtc IS NULL OR StartedAtUtc >= RequestedAtUtc) AND (FinishedAtUtc IS NULL OR StartedAtUtc IS NULL OR FinishedAtUtc >= StartedAtUtc)),
    CONSTRAINT CK_ProvisioningJobs_Version CHECK (Version > 0)
);
CREATE INDEX IX_ProvisioningJobs_Status_RequestedAtUtc ON dbo.ProvisioningJobs (Status, RequestedAtUtc);
CREATE INDEX IX_ProvisioningJobs_BranchDatabase_RequestedAtUtc ON dbo.ProvisioningJobs (BranchDatabaseId, RequestedAtUtc DESC);

CREATE TABLE dbo.ProvisioningSteps
(
    ProvisioningStepId uniqueidentifier NOT NULL,
    ProvisioningJobId uniqueidentifier NOT NULL,
    StepName nvarchar(100) NOT NULL,
    SequenceNumber int NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_ProvisioningSteps_Status DEFAULT (0),
    StartedAtUtc datetime2(7) NULL,
    FinishedAtUtc datetime2(7) NULL,
    AttemptCount int NOT NULL CONSTRAINT DF_ProvisioningSteps_AttemptCount DEFAULT (0),
    ErrorCode nvarchar(100) NULL,
    SafeErrorDetail nvarchar(1000) NULL,
    Version bigint NOT NULL CONSTRAINT DF_ProvisioningSteps_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProvisioningSteps_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProvisioningSteps_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_ProvisioningSteps PRIMARY KEY CLUSTERED (ProvisioningStepId),
    CONSTRAINT FK_ProvisioningSteps_ProvisioningJobs FOREIGN KEY (ProvisioningJobId) REFERENCES dbo.ProvisioningJobs (ProvisioningJobId),
    CONSTRAINT UQ_ProvisioningSteps_Job_Sequence UNIQUE (ProvisioningJobId, SequenceNumber),
    CONSTRAINT CK_ProvisioningSteps_StepName CHECK (LEN(LTRIM(RTRIM(StepName))) > 0),
    CONSTRAINT CK_ProvisioningSteps_SequenceNumber CHECK (SequenceNumber > 0),
    CONSTRAINT CK_ProvisioningSteps_Status CHECK (Status IN (0, 1, 2, 3, 4, 5, 6, 7)),
    CONSTRAINT CK_ProvisioningSteps_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_ProvisioningSteps_Timestamps CHECK (FinishedAtUtc IS NULL OR StartedAtUtc IS NULL OR FinishedAtUtc >= StartedAtUtc),
    CONSTRAINT CK_ProvisioningSteps_Version CHECK (Version > 0)
);
CREATE INDEX IX_ProvisioningSteps_Job_Status ON dbo.ProvisioningSteps (ProvisioningJobId, Status, SequenceNumber);

CREATE TABLE dbo.AuditLogs
(
    AuditLogId uniqueidentifier NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AuditLogs_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
    ActorUserId uniqueidentifier NULL,
    CompanyId uniqueidentifier NULL,
    BranchId uniqueidentifier NULL,
    ActionCode nvarchar(150) NOT NULL,
    EntityType nvarchar(100) NOT NULL,
    EntityId uniqueidentifier NULL,
    Outcome tinyint NOT NULL,
    CorrelationId uniqueidentifier NOT NULL,
    SafeDetailJson nvarchar(max) NULL,
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AuditLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (AuditLogId),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (ActorUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_AuditLogs_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_AuditLogs_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (BranchId),
    CONSTRAINT CK_AuditLogs_ActionCode CHECK (LEN(LTRIM(RTRIM(ActionCode))) > 0),
    CONSTRAINT CK_AuditLogs_EntityType CHECK (LEN(LTRIM(RTRIM(EntityType))) > 0),
    CONSTRAINT CK_AuditLogs_Outcome CHECK (Outcome IN (1, 2, 3)),
    CONSTRAINT CK_AuditLogs_SafeDetailJson CHECK (SafeDetailJson IS NULL OR ISJSON(SafeDetailJson) = 1)
);
CREATE INDEX IX_AuditLogs_Company_OccurredAtUtc ON dbo.AuditLogs (CompanyId, OccurredAtUtc DESC);
CREATE INDEX IX_AuditLogs_Actor_OccurredAtUtc ON dbo.AuditLogs (ActorUserId, OccurredAtUtc DESC);
CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs (EntityType, EntityId, OccurredAtUtc DESC);
CREATE INDEX IX_AuditLogs_CorrelationId ON dbo.AuditLogs (CorrelationId);

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 6, N'006_CreateProvisioningAndAuditTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
