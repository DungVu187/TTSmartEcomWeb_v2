/* Migration 005: số dư, giao dịch và usage AI theo company. */
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
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 5)
BEGIN
    PRINT N'Migration 005 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 4)
    THROW 51005, N'Chưa áp dụng migration 004.', 1;

BEGIN TRANSACTION;

CREATE TABLE dbo.AiBalances
(
    AiBalanceId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    CurrencyCode char(3) NOT NULL,
    AvailableBalance decimal(19,4) NOT NULL CONSTRAINT DF_AiBalances_AvailableBalance DEFAULT (0),
    ReservedBalance decimal(19,4) NOT NULL CONSTRAINT DF_AiBalances_ReservedBalance DEFAULT (0),
    Version bigint NOT NULL CONSTRAINT DF_AiBalances_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiBalances_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiBalances_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_AiBalances PRIMARY KEY CLUSTERED (AiBalanceId),
    CONSTRAINT FK_AiBalances_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT UQ_AiBalances_Company UNIQUE (CompanyId),
    CONSTRAINT CK_AiBalances_CurrencyCode CHECK (CurrencyCode NOT LIKE '%[^A-Z]%'),
    CONSTRAINT CK_AiBalances_AvailableBalance CHECK (AvailableBalance >= 0),
    CONSTRAINT CK_AiBalances_ReservedBalance CHECK (ReservedBalance >= 0),
    CONSTRAINT CK_AiBalances_Version CHECK (Version > 0)
);

CREATE TABLE dbo.AiTransactions
(
    AiTransactionId uniqueidentifier NOT NULL,
    AiBalanceId uniqueidentifier NOT NULL,
    TransactionType tinyint NOT NULL,
    Amount decimal(19,4) NOT NULL,
    BalanceAfter decimal(19,4) NOT NULL,
    ReferenceType nvarchar(100) NULL,
    ReferenceId uniqueidentifier NULL,
    IdempotencyKey nvarchar(128) NOT NULL,
    CreatedByUserId uniqueidentifier NULL,
    OccurredAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiTransactions_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiTransactions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AiTransactions PRIMARY KEY CLUSTERED (AiTransactionId),
    CONSTRAINT FK_AiTransactions_AiBalances FOREIGN KEY (AiBalanceId) REFERENCES dbo.AiBalances (AiBalanceId),
    CONSTRAINT FK_AiTransactions_CreatedByUsers FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_AiTransactions_IdempotencyKey UNIQUE (IdempotencyKey),
    CONSTRAINT CK_AiTransactions_TransactionType CHECK (TransactionType IN (1, 2, 3, 4, 5, 6)),
    CONSTRAINT CK_AiTransactions_Amount CHECK (Amount <> 0),
    CONSTRAINT CK_AiTransactions_BalanceAfter CHECK (BalanceAfter >= 0),
    CONSTRAINT CK_AiTransactions_Reference CHECK ((ReferenceType IS NULL AND ReferenceId IS NULL) OR (ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL))
);
CREATE INDEX IX_AiTransactions_Balance_OccurredAtUtc ON dbo.AiTransactions (AiBalanceId, OccurredAtUtc DESC);

CREATE TABLE dbo.AiUsageLogs
(
    AiUsageLogId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    BranchId uniqueidentifier NULL,
    AiTransactionId uniqueidentifier NULL,
    ProviderCode nvarchar(100) NOT NULL,
    OperationCode nvarchar(100) NOT NULL,
    InputUnits bigint NOT NULL CONSTRAINT DF_AiUsageLogs_InputUnits DEFAULT (0),
    OutputUnits bigint NOT NULL CONSTRAINT DF_AiUsageLogs_OutputUnits DEFAULT (0),
    ChargedAmount decimal(19,4) NOT NULL CONSTRAINT DF_AiUsageLogs_ChargedAmount DEFAULT (0),
    CorrelationId uniqueidentifier NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiUsageLogs_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AiUsageLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AiUsageLogs PRIMARY KEY CLUSTERED (AiUsageLogId),
    CONSTRAINT FK_AiUsageLogs_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_AiUsageLogs_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (BranchId),
    CONSTRAINT FK_AiUsageLogs_AiTransactions FOREIGN KEY (AiTransactionId) REFERENCES dbo.AiTransactions (AiTransactionId),
    CONSTRAINT CK_AiUsageLogs_ProviderCode CHECK (LEN(LTRIM(RTRIM(ProviderCode))) > 0),
    CONSTRAINT CK_AiUsageLogs_OperationCode CHECK (LEN(LTRIM(RTRIM(OperationCode))) > 0),
    CONSTRAINT CK_AiUsageLogs_InputUnits CHECK (InputUnits >= 0),
    CONSTRAINT CK_AiUsageLogs_OutputUnits CHECK (OutputUnits >= 0),
    CONSTRAINT CK_AiUsageLogs_ChargedAmount CHECK (ChargedAmount >= 0)
);
CREATE INDEX IX_AiUsageLogs_Company_OccurredAtUtc ON dbo.AiUsageLogs (CompanyId, OccurredAtUtc DESC);
CREATE INDEX IX_AiUsageLogs_CorrelationId ON dbo.AiUsageLogs (CorrelationId);

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 5, N'005_CreateAiTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
