/*
    DailyMetricList
    The daily values of an account between two days (both included).
*/
CREATE PROCEDURE [dbo].[DailyMetricList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE,
    @To           DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT m.[DayDate], CAST(m.[Metric] AS INT) AS [Metric], m.[Value]
    FROM [dbo].[DailyMetric] m
    INNER JOIN [dbo].[User] u ON u.[UserID] = m.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID AND m.[DayDate] BETWEEN @From AND @To
    ORDER BY m.[DayDate], m.[Metric];
END
