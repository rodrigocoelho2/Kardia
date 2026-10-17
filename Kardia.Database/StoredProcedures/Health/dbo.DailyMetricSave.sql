/*
    DailyMetricSave
    Saves daily values from a sync, as JSON: [{"d":"2026-10-16","m":1,"v":68}, …]. A value synced again replaces the
    old one, except for totals (@SummedMetrics, e.g. steps): the day's total only grows during the day, so the larger
    one is kept and a partial re-send can't lower it.
*/
CREATE PROCEDURE [dbo].[DailyMetricSave]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @Rows          NVARCHAR (MAX),
    @SummedMetrics VARCHAR (100) = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    DECLARE @Summed TABLE ([Metric] TINYINT PRIMARY KEY);
    INSERT INTO @Summed ([Metric])
    SELECT DISTINCT TRY_CAST([value] AS TINYINT) FROM STRING_SPLIT(@SummedMetrics, ',') WHERE TRY_CAST([value] AS TINYINT) IS NOT NULL;

    MERGE [dbo].[DailyMetric] WITH (HOLDLOCK) AS target
    USING (
        SELECT [DayDate], [Metric], MAX([Value]) AS [Value]
        FROM OPENJSON(@Rows) WITH ([DayDate] DATE '$.d', [Metric] TINYINT '$.m', [Value] DECIMAL (10, 2) '$.v')
        GROUP BY [DayDate], [Metric]
    ) AS source
    ON target.[UserID] = @UserID AND target.[DayDate] = source.[DayDate] AND target.[Metric] = source.[Metric]
    WHEN MATCHED THEN
        UPDATE SET [Value] = CASE WHEN EXISTS (SELECT 1 FROM @Summed s WHERE s.[Metric] = source.[Metric]) AND target.[Value] > source.[Value]
                                  THEN target.[Value] ELSE source.[Value] END,
                   [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [DayDate], [Metric], [Value]) VALUES (@UserID, source.[DayDate], source.[Metric], source.[Value]);
END
