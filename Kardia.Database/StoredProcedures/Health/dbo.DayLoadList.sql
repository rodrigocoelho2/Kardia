/*
    DayLoadList
    The heart load of an account's days between two days (both included).
*/
CREATE PROCEDURE [dbo].[DayLoadList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE,
    @To           DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT l.[DayDate], l.[Trimp], l.[ZoneSeconds], l.[MaxBpm], l.[AvgBpm], l.[Minutes]
    FROM [dbo].[DayLoad] l
    INNER JOIN [dbo].[User] u ON u.[UserID] = l.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID AND l.[DayDate] BETWEEN @From AND @To
    ORDER BY l.[DayDate];
END
