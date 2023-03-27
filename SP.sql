/****** Object:  StoredProcedure [dbo].[GetLivevoxDownloadCallAPIStatus]    Script Date: 3/27/2023 11:11:25 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[GetLivevoxDownloadCallAPIStatus]
    @Time_Interval INT,
    @Time_Threshold INT
AS
BEGIN
    SELECT ClientName, SourceName, SynapseTriggerId, Status, LastActivityOn
    FROM LivevoxDownloadCallAPIStatus
    WHERE LastActivityOn > DATEADD(minute, @Time_Interval, GETDATE())
        AND SynapseTriggerId LIKE '%_Ingestion'
        AND (Status LIKE '%Completed' OR DATEDIFF(hour, CreatedOn, GETDATE()) > @Time_Threshold)
END
GO




/****** Object:  StoredProcedure [dbo].[GetCallMetadataStatistics]    Script Date: 3/27/2023 11:12:03 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[GetCallMetadataStatistics]
    @ClientName varchar(100),
    @ClientSource varchar(100)
AS
BEGIN
    SELECT 
        COUNT(*) TotalRecords,
        COUNT(CASE WHEN Status LIKE '%Discard%' THEN Status END) TotalDiscarded,
        COUNT(CASE WHEN MergeStatus NOT IN ('Discard', 'FilterDiscard') THEN MergeStatus END) TotalRecordsForIngestion,
        COUNT(CASE WHEN Status = 'Posted' THEN Status END) Ingested,
        COUNT(CASE WHEN Status = 'Failed' AND ISNULL(PostReTryCount, 0) >= 10 THEN Status END) IngestionFailed,
        COUNT(CASE WHEN Status = 'Pending' OR (Status = 'Failed' AND ISNULL(PostReTryCount, 0) < 10) THEN Status END) IngestionPending,
        COUNT(CASE WHEN Status = 'Discard' THEN Status END) AudioDurationDiscard,
        COUNT(CASE WHEN Status = 'FilterDiscard' THEN Status END) FilterDiscard,
        COUNT(CASE WHEN ClientCaptureDate IS NULL OR ClientCaptureDate = '' THEN ClientCaptureDate END) InvalidClientCaptureDate,
        COUNT(CASE WHEN ClientID IS NULL OR ClientID = '' THEN ClientID END) InvalidClientID,
        COUNT(CASE WHEN OutputAudioFileName IS NULL OR OutputAudioFileName = '' OR OutputAudioFileName NOT LIKE '%.%' THEN OutputAudioFileName END) InvalidOutputAudioFileName
    FROM callmetadata 
    WHERE ClientName = @ClientName AND ClientSource = @ClientSource
END
GO




