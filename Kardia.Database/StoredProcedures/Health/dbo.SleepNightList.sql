/*
    SleepNightList
    The nights of an account whose morning falls between two days (both included).
*/
CREATE PROCEDURE [dbo].[SleepNightList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE,
    @To           DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[NightDate], s.[BedStart], s.[BedEnd], s.[AwakeMinutes], s.[RemMinutes], s.[CoreMinutes], s.[DeepMinutes], s.[Stages]
    FROM [dbo].[SleepNight] s
    INNER JOIN [dbo].[User] u ON u.[UserID] = s.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID AND s.[NightDate] BETWEEN @From AND @To
    ORDER BY s.[NightDate];
END
