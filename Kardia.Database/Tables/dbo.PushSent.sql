CREATE TABLE [dbo].[PushSent]
(
    [UserID]  BIGINT        NOT NULL,
    [SentKey] NVARCHAR (80) NOT NULL,
    [SentAt]  DATETIME2 (0) NOT NULL CONSTRAINT [DF_PushSent_SentAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_PushSent] PRIMARY KEY CLUSTERED ([UserID] ASC, [SentKey] ASC),
    CONSTRAINT [FK_PushSent_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The notifications already sent ("morning:2026-10-16", "week:2026-10-12"), so each goes out once even after a restart. Rows older than 45 days are removed as new ones are claimed.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PushSent';
