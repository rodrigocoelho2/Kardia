CREATE TABLE [dbo].[UserSetting]
(
    [UserID]             BIGINT        NOT NULL,
    [SleepGoalPercent]   TINYINT       NOT NULL CONSTRAINT [DF_UserSetting_SleepGoalPercent] DEFAULT (100),
    [ActiveMinutesWeek]  SMALLINT      NOT NULL CONSTRAINT [DF_UserSetting_ActiveMinutesWeek] DEFAULT (150),
    [StepsDay]           INT           NOT NULL CONSTRAINT [DF_UserSetting_StepsDay] DEFAULT (8000),
    [SleepNightsWeek]    TINYINT       NOT NULL CONSTRAINT [DF_UserSetting_SleepNightsWeek] DEFAULT (5),
    [BedtimeNightsWeek]  TINYINT       NOT NULL CONSTRAINT [DF_UserSetting_BedtimeNightsWeek] DEFAULT (5),
    [NotifyMorning]      BIT           NOT NULL CONSTRAINT [DF_UserSetting_NotifyMorning] DEFAULT (1),
    [NotifyBedtime]      BIT           NOT NULL CONSTRAINT [DF_UserSetting_NotifyBedtime] DEFAULT (1),
    [BedtimeLeadMinutes] SMALLINT      NOT NULL CONSTRAINT [DF_UserSetting_BedtimeLeadMinutes] DEFAULT (30),
    [NotifyHealth]       BIT           NOT NULL CONSTRAINT [DF_UserSetting_NotifyHealth] DEFAULT (1),
    [NotifyStrain]       BIT           NOT NULL CONSTRAINT [DF_UserSetting_NotifyStrain] DEFAULT (0),
    [NotifyReports]      BIT           NOT NULL CONSTRAINT [DF_UserSetting_NotifyReports] DEFAULT (1),
    [UpdatedAt]          DATETIME2 (0) NOT NULL CONSTRAINT [DF_UserSetting_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_UserSetting] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_UserSetting_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserSetting_SleepGoalPercent] CHECK ([SleepGoalPercent] IN (70, 85, 100))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Goals and notification choices: the sleep goal (100, 85 or 70 % of the need), the weekly plan targets (zone 2+ minutes, daily steps, nights on the sleep goal, nights in bed on time) and which push notifications to send. No row = the defaults.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UserSetting';
