/*
    WorkoutSave
    Saves workouts from a sync, as JSON: [{"s":"…T08:30:00","e":"…T09:08:00","k":"Outdoor Run","kc":512,"km":7.2,"av":156,"mx":182}, …].
    A workout synced again (same start) replaces the old one.
*/
CREATE PROCEDURE [dbo].[WorkoutSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[Workout] WITH (HOLDLOCK) AS target
    USING (
        SELECT *
        FROM OPENJSON(@Rows) WITH (
            [StartedAt] DATETIME2 (0) '$.s', [EndedAt] DATETIME2 (0) '$.e', [Kind] NVARCHAR (60) '$.k',
            [ActiveKcal] DECIMAL (7, 1) '$.kc', [DistanceKm] DECIMAL (7, 2) '$.km', [AvgBpm] SMALLINT '$.av', [MaxBpm] SMALLINT '$.mx')
    ) AS source
    ON target.[UserID] = @UserID AND target.[StartedAt] = source.[StartedAt]
    WHEN MATCHED THEN
        UPDATE SET [EndedAt] = source.[EndedAt], [Kind] = source.[Kind], [ActiveKcal] = source.[ActiveKcal],
                   [DistanceKm] = source.[DistanceKm], [AvgBpm] = source.[AvgBpm], [MaxBpm] = source.[MaxBpm]
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [StartedAt], [EndedAt], [Kind], [ActiveKcal], [DistanceKm], [AvgBpm], [MaxBpm])
        VALUES (@UserID, source.[StartedAt], source.[EndedAt], source.[Kind], source.[ActiveKcal], source.[DistanceKm], source.[AvgBpm], source.[MaxBpm]);
END
