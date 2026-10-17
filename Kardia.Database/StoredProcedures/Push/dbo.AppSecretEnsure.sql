/*
    AppSecretEnsure
    Returns the value kept under @Name, saving @Value first when there is none (the first caller wins, so two
    containers starting together end up with the same push keys).
*/
CREATE PROCEDURE [dbo].[AppSecretEnsure]
    @Name  NVARCHAR (50),
    @Value NVARCHAR (1000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO [dbo].[AppSecret] ([Name], [Value])
    SELECT @Name, @Value
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[AppSecret] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = @Name);

    SELECT [Value] FROM [dbo].[AppSecret] WHERE [Name] = @Name;
END
