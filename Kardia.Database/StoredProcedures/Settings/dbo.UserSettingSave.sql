/*
    UserSettingSave
    Creates or replaces the goals and notification choices of an account.
*/
CREATE PROCEDURE [dbo].[UserSettingSave]
    @UserUniqueID       UNIQUEIDENTIFIER,
    @SleepGoalPercent   TINYINT,
    @ActiveMinutesWeek  SMALLINT,
    @StepsDay           INT,
    @SleepNightsWeek    TINYINT,
    @BedtimeNightsWeek  TINYINT,
    @NotifyMorning      BIT,
    @NotifyBedtime      BIT,
    @BedtimeLeadMinutes SMALLINT,
    @NotifyHealth       BIT,
    @NotifyStrain       BIT,
    @NotifyReports      BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[UserSetting] WITH (HOLDLOCK) AS target
    USING (SELECT @UserID AS [UserID]) AS source
    ON target.[UserID] = source.[UserID]
    WHEN MATCHED THEN
        UPDATE SET [SleepGoalPercent] = @SleepGoalPercent, [ActiveMinutesWeek] = @ActiveMinutesWeek, [StepsDay] = @StepsDay,
                   [SleepNightsWeek] = @SleepNightsWeek, [BedtimeNightsWeek] = @BedtimeNightsWeek, [NotifyMorning] = @NotifyMorning,
                   [NotifyBedtime] = @NotifyBedtime, [BedtimeLeadMinutes] = @BedtimeLeadMinutes, [NotifyHealth] = @NotifyHealth,
                   [NotifyStrain] = @NotifyStrain, [NotifyReports] = @NotifyReports, [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [SleepGoalPercent], [ActiveMinutesWeek], [StepsDay], [SleepNightsWeek], [BedtimeNightsWeek], [NotifyMorning],
                [NotifyBedtime], [BedtimeLeadMinutes], [NotifyHealth], [NotifyStrain], [NotifyReports])
        VALUES (@UserID, @SleepGoalPercent, @ActiveMinutesWeek, @StepsDay, @SleepNightsWeek, @BedtimeNightsWeek, @NotifyMorning,
                @NotifyBedtime, @BedtimeLeadMinutes, @NotifyHealth, @NotifyStrain, @NotifyReports);
END
