/*
    SyncKeySave
    Saves a new sync key for an account, replacing the old one (which stops working).
*/
CREATE PROCEDURE [dbo].[SyncKeySave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @KeyHash      BINARY (32),
    @KeyHint      NVARCHAR (24)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[SyncKey] WHERE [UserID] = @UserID;

    INSERT INTO [dbo].[SyncKey] ([UserID], [KeyHash], [KeyHint])
    VALUES (@UserID, @KeyHash, @KeyHint);

    COMMIT TRANSACTION;
END
