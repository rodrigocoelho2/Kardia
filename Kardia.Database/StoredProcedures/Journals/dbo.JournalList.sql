/*
    JournalList
    The habits an account marked between two days (both included).
*/
CREATE PROCEDURE [dbo].[JournalList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @From         DATE,
    @To           DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT j.[DayDate], j.[Tag]
    FROM [dbo].[JournalEntry] j
    INNER JOIN [dbo].[User] u ON u.[UserID] = j.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID AND j.[DayDate] BETWEEN @From AND @To
    ORDER BY j.[DayDate], j.[Tag];
END
