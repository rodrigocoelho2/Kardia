CREATE TABLE [dbo].[HeartRateMinute]
(
    [UserID]   BIGINT        NOT NULL,
    [MinuteAt] DATETIME2 (0) NOT NULL,
    [Bpm]      SMALLINT      NOT NULL,
    CONSTRAINT [PK_HeartRateMinute] PRIMARY KEY CLUSTERED ([UserID] ASC, [MinuteAt] ASC),
    CONSTRAINT [FK_HeartRateMinute_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
) WITH (DATA_COMPRESSION = PAGE);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Heart rate, one row per minute (local time of the app), the source of strain and heart-rate zones. The largest table: about 1 440 rows per person per day, page-compressed.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'HeartRateMinute';
