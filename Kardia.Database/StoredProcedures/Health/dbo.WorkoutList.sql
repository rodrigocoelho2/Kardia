/*
    WorkoutList
    The workouts of an account that started between two days (both included).
*/
CREATE PROCEDURE [dbo].[WorkoutList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE,
    @To           DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);

    SELECT w.[StartedAt], w.[EndedAt], w.[Kind], w.[ActiveKcal], w.[DistanceKm], w.[AvgBpm], w.[MaxBpm]
    FROM [dbo].[Workout] w
    WHERE w.[UserID] = @UserID AND w.[StartedAt] >= CAST(@From AS DATETIME2 (0)) AND w.[StartedAt] < DATEADD(DAY, 1, CAST(@To AS DATETIME2 (0)))
    ORDER BY w.[StartedAt];
END
