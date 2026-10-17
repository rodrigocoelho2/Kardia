/*
    SyncKeyMark
    Records what the last sync brought ("2 days · 1 night · 1 workout…"), shown in Settings.
*/
CREATE PROCEDURE [dbo].[SyncKeyMark]
    @UserUniqueID UNIQUEIDENTIFIER,
    @LastSummary  NVARCHAR (200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE k
    SET k.[LastSummary] = ISNULL(@LastSummary, N'')
    FROM [dbo].[SyncKey] k
    INNER JOIN [dbo].[User] u ON u.[UserID] = k.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
