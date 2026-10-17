/*
    UserSettingGet
    The goals and notification choices of an account (no row until first saved: the WebApi uses the defaults).
*/
CREATE PROCEDURE [dbo].[UserSettingGet]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[SleepGoalPercent],
           s.[ActiveMinutesWeek],
           s.[StepsDay],
           s.[SleepNightsWeek],
           s.[BedtimeNightsWeek],
           s.[NotifyMorning],
           s.[NotifyBedtime],
           s.[BedtimeLeadMinutes],
           s.[NotifyHealth],
           s.[NotifyStrain],
           s.[NotifyReports]
    FROM [dbo].[UserSetting] s
    INNER JOIN [dbo].[User] u ON u.[UserID] = s.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
