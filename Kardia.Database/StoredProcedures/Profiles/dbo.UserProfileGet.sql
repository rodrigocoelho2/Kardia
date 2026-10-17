/*
    UserProfileGet
    The profile of an account (no row until it is first saved).
*/
CREATE PROCEDURE [dbo].[UserProfileGet]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT p.[BirthDate],
           p.[Sex],
           p.[HeightCm],
           p.[WeightKg],
           p.[MaxHeartRate],
           p.[SleepNeedMinutes]
    FROM [dbo].[UserProfile] p
    INNER JOIN [dbo].[User] u ON u.[UserID] = p.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
