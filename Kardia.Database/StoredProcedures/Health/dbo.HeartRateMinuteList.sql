/*
    HeartRateMinuteList
    Heart rate per minute of an account from @From (included) to @To (excluded).
*/
CREATE PROCEDURE [dbo].[HeartRateMinuteList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATETIME2 (0),
    @To           DATETIME2 (0)
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);

    SELECT h.[MinuteAt], h.[Bpm]
    FROM [dbo].[HeartRateMinute] h
    WHERE h.[UserID] = @UserID AND h.[MinuteAt] >= @From AND h.[MinuteAt] < @To
    ORDER BY h.[MinuteAt];
END
