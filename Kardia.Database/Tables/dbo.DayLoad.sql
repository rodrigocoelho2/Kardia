CREATE TABLE [dbo].[DayLoad]
(
    [UserID]      BIGINT          NOT NULL,
    [DayDate]     DATE            NOT NULL,
    [Trimp]       DECIMAL (9, 2)  NOT NULL,
    [ZoneSeconds] NVARCHAR (60)   NOT NULL,
    [MaxBpm]      SMALLINT        NULL,
    [AvgBpm]      SMALLINT        NULL,
    [Minutes]     SMALLINT        NOT NULL,
    [UpdatedAt]   DATETIME2 (0)   NOT NULL CONSTRAINT [DF_DayLoad_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_DayLoad] PRIMARY KEY CLUSTERED ([UserID] ASC, [DayDate] ASC),
    CONSTRAINT [FK_DayLoad_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The heart load of each day, worked out from HeartRateMinute after every sync (Banister TRIMP and seconds in zones 1–5 as a,b,c,d,e) so the screens never read a whole day of minutes.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DayLoad';
