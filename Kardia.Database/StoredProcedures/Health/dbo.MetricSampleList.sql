/*
    MetricSampleList
    Timed readings (HRV, respiratory rate, blood oxygen) of an account from @From (included) to @To (excluded).
*/
CREATE PROCEDURE [dbo].[MetricSampleList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATETIME2 (0),
    @To           DATETIME2 (0)
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);

    SELECT s.[Metric], s.[SampleAt], s.[Value]
    FROM [dbo].[MetricSample] s
    WHERE s.[UserID] = @UserID AND s.[SampleAt] >= @From AND s.[SampleAt] < @To
    ORDER BY s.[SampleAt];
END
