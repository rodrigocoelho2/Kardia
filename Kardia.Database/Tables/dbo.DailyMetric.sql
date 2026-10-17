CREATE TABLE [dbo].[DailyMetric]
(
    [UserID]    BIGINT          NOT NULL,
    [DayDate]   DATE            NOT NULL,
    [Metric]    TINYINT         NOT NULL,
    [Value]     DECIMAL (10, 2) NOT NULL,
    [UpdatedAt] DATETIME2 (0)   NOT NULL CONSTRAINT [DF_DailyMetric_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_DailyMetric] PRIMARY KEY CLUSTERED ([UserID] ASC, [DayDate] ASC, [Metric] ASC),
    CONSTRAINT [FK_DailyMetric_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'One value per day and metric from Apple Health (Kardia.Library HealthMetric: 1 HRV, 2 resting heart rate, 3 respiratory rate, 4 blood oxygen, 5 wrist temperature, 6 steps, 7 active energy, 8 basal energy, 9 VO2 max, 10 walking heart rate, 11 body mass, 12 exercise minutes).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DailyMetric';
