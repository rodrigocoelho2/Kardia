/*
    DayLoadSave
    Saves the heart load of days, as JSON: [{"d":"2026-10-16","t":182.4,"z":"2460,300,60,1920,0","mx":164,"av":66,"mi":1050}, …].
*/
CREATE PROCEDURE [dbo].[DayLoadSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[DayLoad] WITH (HOLDLOCK) AS target
    USING (
        SELECT *
        FROM OPENJSON(@Rows) WITH (
            [DayDate] DATE '$.d', [Trimp] DECIMAL (9, 2) '$.t', [ZoneSeconds] NVARCHAR (60) '$.z',
            [MaxBpm] SMALLINT '$.mx', [AvgBpm] SMALLINT '$.av', [Minutes] SMALLINT '$.mi')
    ) AS source
    ON target.[UserID] = @UserID AND target.[DayDate] = source.[DayDate]
    WHEN MATCHED THEN
        UPDATE SET [Trimp] = source.[Trimp], [ZoneSeconds] = source.[ZoneSeconds], [MaxBpm] = source.[MaxBpm],
                   [AvgBpm] = source.[AvgBpm], [Minutes] = source.[Minutes], [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [DayDate], [Trimp], [ZoneSeconds], [MaxBpm], [AvgBpm], [Minutes])
        VALUES (@UserID, source.[DayDate], source.[Trimp], source.[ZoneSeconds], source.[MaxBpm], source.[AvgBpm], source.[Minutes]);
END
