/*
    PushSubscriptionSave
    Adds this browser, or updates it (same endpoint): new keys, device name, the account now signed in, no error.
*/
CREATE PROCEDURE [dbo].[PushSubscriptionSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Endpoint     NVARCHAR (800),
    @P256dh       NVARCHAR (200),
    @Auth         NVARCHAR (100),
    @Device       NVARCHAR (60)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    DECLARE @Hash BINARY (32) = CAST(HASHBYTES('SHA2_256', @Endpoint) AS BINARY (32));

    BEGIN TRANSACTION;

    UPDATE [dbo].[PushSubscription] WITH (UPDLOCK, HOLDLOCK)
    SET [UserID] = @UserID,
        [P256dh] = @P256dh,
        [Auth] = @Auth,
        [Device] = @Device,
        [LastError] = N''
    WHERE [EndpointHash] = @Hash;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[PushSubscription] ([UserID], [Endpoint], [P256dh], [Auth], [Device])
        VALUES (@UserID, @Endpoint, @P256dh, @Auth, @Device);
    END

    COMMIT TRANSACTION;
END
