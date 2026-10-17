/*
    PushSubscriptionList
    The devices with notifications on: one account's (@UserUniqueID), or every account's (NULL, for the worker).
*/
CREATE PROCEDURE [dbo].[PushSubscriptionList]
    @UserUniqueID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[PushSubscriptionID],
           u.[UserUniqueID],
           s.[Endpoint],
           s.[P256dh],
           s.[Auth],
           s.[Device],
           s.[CreatedAt],
           s.[LastSentAt],
           s.[LastError]
    FROM [dbo].[PushSubscription] s
    INNER JOIN [dbo].[User] u ON u.[UserID] = s.[UserID]
    WHERE @UserUniqueID IS NULL OR u.[UserUniqueID] = @UserUniqueID
    ORDER BY s.[UserID], s.[CreatedAt] DESC;
END
