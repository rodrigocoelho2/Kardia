/*
    JournalSet
    Marks (@IsOn = 1) or unmarks a habit on a day.
*/
CREATE PROCEDURE [dbo].[JournalSet]
    @UserUniqueID UNIQUEIDENTIFIER,
    @DayDate      DATE,
    @Tag          VARCHAR (32),
    @IsOn         BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    IF @IsOn = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [dbo].[JournalEntry] WITH (UPDLOCK, HOLDLOCK) WHERE [UserID] = @UserID AND [DayDate] = @DayDate AND [Tag] = @Tag)
            INSERT INTO [dbo].[JournalEntry] ([UserID], [DayDate], [Tag]) VALUES (@UserID, @DayDate, @Tag);
    END
    ELSE
    BEGIN
        DELETE FROM [dbo].[JournalEntry] WHERE [UserID] = @UserID AND [DayDate] = @DayDate AND [Tag] = @Tag;
    END
END
