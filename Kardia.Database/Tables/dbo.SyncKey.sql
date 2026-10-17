CREATE TABLE [dbo].[SyncKey]
(
    [UserID]      BIGINT         NOT NULL,
    [KeyHash]     BINARY (32)    NOT NULL,
    [KeyHint]     NVARCHAR (24)  NOT NULL,
    [CreatedAt]   DATETIME2 (0)  NOT NULL CONSTRAINT [DF_SyncKey_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [LastUsedAt]  DATETIME2 (0)  NULL,
    [LastSummary] NVARCHAR (200) NOT NULL CONSTRAINT [DF_SyncKey_LastSummary] DEFAULT (N''),
    CONSTRAINT [PK_SyncKey] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [UQ_SyncKey_KeyHash] UNIQUE NONCLUSTERED ([KeyHash] ASC),
    CONSTRAINT [FK_SyncKey_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The personal key Health Auto Export or the Kardia Shortcut sends (X-Kardia-Key). Only its SHA-256 is kept; one key per account, a new one replaces the old.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'SyncKey';
