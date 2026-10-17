/*
    SyncKeyUse
    Finds the account a sync key belongs to and records the use. Returns its UserUniqueID, or no row for an unknown key.
*/
CREATE PROCEDURE [dbo].[SyncKeyUse]
    @KeyHash BINARY (32)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Used TABLE ([UserID] BIGINT NOT NULL);

    UPDATE [dbo].[SyncKey]
    SET [LastUsedAt] = SYSUTCDATETIME()
    OUTPUT inserted.[UserID] INTO @Used
    WHERE [KeyHash] = @KeyHash;

    SELECT u.[UserUniqueID]
    FROM @Used x
    INNER JOIN [dbo].[User] u ON u.[UserID] = x.[UserID];
END
