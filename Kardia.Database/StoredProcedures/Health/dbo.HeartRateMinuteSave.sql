/*
    HeartRateMinuteSave
    Saves heart rate per minute from a sync, as JSON: [{"t":"2026-10-16T08:30:00","b":152}, …]. A minute synced again
    replaces the old value.
*/
CREATE PROCEDURE [dbo].[HeartRateMinuteSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[HeartRateMinute] WITH (HOLDLOCK) AS target
    USING (
        SELECT [MinuteAt], MAX([Bpm]) AS [Bpm]
        FROM OPENJSON(@Rows) WITH ([MinuteAt] DATETIME2 (0) '$.t', [Bpm] SMALLINT '$.b')
        GROUP BY [MinuteAt]
    ) AS source
    ON target.[UserID] = @UserID AND target.[MinuteAt] = source.[MinuteAt]
    WHEN MATCHED AND target.[Bpm] <> source.[Bpm] THEN
        UPDATE SET [Bpm] = source.[Bpm]
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [MinuteAt], [Bpm]) VALUES (@UserID, source.[MinuteAt], source.[Bpm]);
END
