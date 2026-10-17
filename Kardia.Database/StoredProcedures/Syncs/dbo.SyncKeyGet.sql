/*
    SyncKeyGet
    The sync key of an account (no row when there is none).
*/
CREATE PROCEDURE [dbo].[SyncKeyGet]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT k.[KeyHash], k.[KeyHint], k.[CreatedAt], k.[LastUsedAt], k.[LastSummary]
    FROM [dbo].[SyncKey] k
    INNER JOIN [dbo].[User] u ON u.[UserID] = k.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
