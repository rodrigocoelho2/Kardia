/*
    MetricSampleSave
    Saves timed readings from a sync, as JSON: [{"m":1,"t":"2026-10-16T03:12:00","v":64.2}, …]. A reading synced again
    replaces the old value.
*/
CREATE PROCEDURE [dbo].[MetricSampleSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[MetricSample] WITH (HOLDLOCK) AS target
    USING (
        SELECT [Metric], [SampleAt], AVG([Value]) AS [Value]
        FROM OPENJSON(@Rows) WITH ([Metric] TINYINT '$.m', [SampleAt] DATETIME2 (0) '$.t', [Value] DECIMAL (10, 3) '$.v')
        GROUP BY [Metric], [SampleAt]
    ) AS source
    ON target.[UserID] = @UserID AND target.[SampleAt] = source.[SampleAt] AND target.[Metric] = source.[Metric]
    WHEN MATCHED AND target.[Value] <> source.[Value] THEN
        UPDATE SET [Value] = source.[Value]
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [Metric], [SampleAt], [Value]) VALUES (@UserID, source.[Metric], source.[SampleAt], source.[Value]);
END
