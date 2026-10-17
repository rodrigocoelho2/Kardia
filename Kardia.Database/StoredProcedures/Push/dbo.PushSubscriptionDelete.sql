/*
    PushSubscriptionDelete
    Removes a device: one of the account's by id (@UserUniqueID and @PushSubscriptionID), or by endpoint (@Endpoint,
    this browser turning notifications off, or the push service saying it's gone).
*/
CREATE PROCEDURE [dbo].[PushSubscriptionDelete]
    @UserUniqueID       UNIQUEIDENTIFIER = NULL,
    @PushSubscriptionID BIGINT = NULL,
    @Endpoint           NVARCHAR (800) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DELETE s
    FROM [dbo].[PushSubscription] s
    LEFT JOIN [dbo].[User] u ON u.[UserID] = s.[UserID]
    WHERE (@PushSubscriptionID IS NOT NULL AND s.[PushSubscriptionID] = @PushSubscriptionID AND u.[UserUniqueID] = @UserUniqueID)
       OR (@Endpoint IS NOT NULL AND s.[EndpointHash] = CAST(HASHBYTES('SHA2_256', @Endpoint) AS BINARY (32)));
END
