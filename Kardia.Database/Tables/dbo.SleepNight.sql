CREATE TABLE [dbo].[SleepNight]
(
    [UserID]       BIGINT          NOT NULL,
    [NightDate]    DATE            NOT NULL,
    [BedStart]     DATETIME2 (0)   NOT NULL,
    [BedEnd]       DATETIME2 (0)   NOT NULL,
    [AwakeMinutes] SMALLINT        NOT NULL,
    [RemMinutes]   SMALLINT        NOT NULL,
    [CoreMinutes]  SMALLINT        NOT NULL,
    [DeepMinutes]  SMALLINT        NOT NULL,
    [Stages]       NVARCHAR (MAX)  NOT NULL CONSTRAINT [DF_SleepNight_Stages] DEFAULT (N''),
    [UpdatedAt]    DATETIME2 (0)   NOT NULL CONSTRAINT [DF_SleepNight_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_SleepNight] PRIMARY KEY CLUSTERED ([UserID] ASC, [NightDate] ASC),
    CONSTRAINT [FK_SleepNight_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'One night per morning (NightDate is the day you woke up): in bed from BedStart to BedEnd, minutes per stage, and the stages in order as stage,startMinute,endMinute;… (empty when only totals were synced).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'SleepNight';
