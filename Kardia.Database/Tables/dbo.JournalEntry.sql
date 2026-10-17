CREATE TABLE [dbo].[JournalEntry]
(
    [UserID]    BIGINT        NOT NULL,
    [DayDate]   DATE          NOT NULL,
    [Tag]       VARCHAR (32)  NOT NULL,
    [CreatedAt] DATETIME2 (0) NOT NULL CONSTRAINT [DF_JournalEntry_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_JournalEntry] PRIMARY KEY CLUSTERED ([UserID] ASC, [DayDate] ASC, [Tag] ASC),
    CONSTRAINT [FK_JournalEntry_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Habits marked in the journal (Kardia.Library JournalTag names: Alcohol, LateCaffeine, Stretching…). A row means the habit happened that day.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'JournalEntry';
