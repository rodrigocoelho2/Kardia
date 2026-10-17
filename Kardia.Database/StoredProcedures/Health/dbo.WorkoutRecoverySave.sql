/*
    WorkoutRecoverySave
    Saves the heart-rate recovery of workouts, as JSON: [{"s":"2026-10-16T08:30:00","h1":28,"h2":45}, …] (start of the
    workout, bpm fallen after one and two minutes; null when there weren't enough heart-rate minutes).
*/
CREATE PROCEDURE [dbo].[WorkoutRecoverySave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    UPDATE w
    SET w.[Hrr1] = source.[Hrr1], w.[Hrr2] = source.[Hrr2]
    FROM [dbo].[Workout] w
    INNER JOIN OPENJSON(@Rows) WITH ([StartedAt] DATETIME2 (0) '$.s', [Hrr1] SMALLINT '$.h1', [Hrr2] SMALLINT '$.h2') AS source
        ON w.[UserID] = @UserID AND w.[StartedAt] = source.[StartedAt];
END
