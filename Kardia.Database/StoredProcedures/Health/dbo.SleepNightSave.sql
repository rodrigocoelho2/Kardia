/*
    SleepNightSave
    Saves nights from a sync, as JSON: [{"n":"2026-10-16","s":"…T23:48:00","e":"…T07:52:00","aw":24,"re":130,"co":261,"de":69,
    "st":"2,0,32;3,32,77;…"}, …]. A night synced again replaces the old one.
*/
CREATE PROCEDURE [dbo].[SleepNightSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[SleepNight] WITH (HOLDLOCK) AS target
    USING (
        SELECT *
        FROM OPENJSON(@Rows) WITH (
            [NightDate] DATE '$.n', [BedStart] DATETIME2 (0) '$.s', [BedEnd] DATETIME2 (0) '$.e',
            [AwakeMinutes] SMALLINT '$.aw', [RemMinutes] SMALLINT '$.re', [CoreMinutes] SMALLINT '$.co', [DeepMinutes] SMALLINT '$.de',
            [Stages] NVARCHAR (MAX) '$.st')
    ) AS source
    ON target.[UserID] = @UserID AND target.[NightDate] = source.[NightDate]
    WHEN MATCHED THEN
        UPDATE SET [BedStart] = source.[BedStart], [BedEnd] = source.[BedEnd], [AwakeMinutes] = source.[AwakeMinutes],
                   [RemMinutes] = source.[RemMinutes], [CoreMinutes] = source.[CoreMinutes], [DeepMinutes] = source.[DeepMinutes],
                   [Stages] = ISNULL(source.[Stages], N''), [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [NightDate], [BedStart], [BedEnd], [AwakeMinutes], [RemMinutes], [CoreMinutes], [DeepMinutes], [Stages])
        VALUES (@UserID, source.[NightDate], source.[BedStart], source.[BedEnd], source.[AwakeMinutes], source.[RemMinutes],
                source.[CoreMinutes], source.[DeepMinutes], ISNULL(source.[Stages], N''));
END
