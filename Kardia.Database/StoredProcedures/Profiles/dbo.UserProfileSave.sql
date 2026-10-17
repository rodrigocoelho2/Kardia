/*
    UserProfileSave
    Creates or replaces the profile of an account.
*/
CREATE PROCEDURE [dbo].[UserProfileSave]
    @UserUniqueID     UNIQUEIDENTIFIER,
    @BirthDate        DATE = NULL,
    @Sex              TINYINT = 0,
    @HeightCm         DECIMAL (5, 1) = NULL,
    @WeightKg         DECIMAL (5, 1) = NULL,
    @MaxHeartRate     SMALLINT = NULL,
    @SleepNeedMinutes SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[UserProfile] WITH (HOLDLOCK) AS target
    USING (SELECT @UserID AS [UserID]) AS source
    ON target.[UserID] = source.[UserID]
    WHEN MATCHED THEN
        UPDATE SET [BirthDate] = @BirthDate, [Sex] = ISNULL(@Sex, 0), [HeightCm] = @HeightCm, [WeightKg] = @WeightKg,
                   [MaxHeartRate] = @MaxHeartRate, [SleepNeedMinutes] = @SleepNeedMinutes, [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [BirthDate], [Sex], [HeightCm], [WeightKg], [MaxHeartRate], [SleepNeedMinutes])
        VALUES (@UserID, @BirthDate, ISNULL(@Sex, 0), @HeightCm, @WeightKg, @MaxHeartRate, @SleepNeedMinutes);
END
