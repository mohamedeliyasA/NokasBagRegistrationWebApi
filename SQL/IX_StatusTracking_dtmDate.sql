USE [Nattsafe]
GO
/****** StatusTracking is a heap with no indexes - this speeds up watermark-based reads of
        recently changed rows without a full table scan. Consider ONLINE = ON if the edition
        supports it, to avoid blocking concurrent writes during creation given the table's
        write volume.
 ******/
CREATE NONCLUSTERED INDEX [IX_StatusTracking_dtmDate]
ON [dbo].[StatusTracking] ([dtmDate])
INCLUDE ([intId], [intStatus]);
GO
