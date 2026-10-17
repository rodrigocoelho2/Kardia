/*
    SyncKeyDelete
    Revokes the sync key of an account.
*/
CREATE PROCEDURE [dbo].[SyncKeyDelete]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE k
    FROM [dbo].[SyncKey] k
    INNER JOIN [dbo].[User] u ON u.[UserID] = k.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
