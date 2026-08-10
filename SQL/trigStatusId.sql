USE [Nattsafe]
GO
/****** Object:  Trigger [dbo].[trigStatusId]    Script Date: 8/8/2026 10:20:37 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER TRIGGER [dbo].[trigStatusId] ON [dbo].[Nattsafearkiv] 
FOR INSERT, UPDATE 
AS
If UPDATE(intStatus)
Begin
--Oppdater tracking
  Insert into StatusTracking (
	intId
	,intStatus
	,dtmDate)
  Select
	intId
	,intStatus
	,getdate()
  From Inserted
  --Nullstiller overføring til Transfleet dersom dette er en ny status for Transfleet
  If Exists(Select deleted.intId
	  from deleted
	  inner join Status as StatusBefore on deleted.intStatus = StatusBefore.intStatus_id
	  inner join inserted on deleted.intId = inserted.intId
	  inner join Status as StatusAfter on inserted.intStatus = StatusAfter.intStatus_id
	  Where StatusBefore.vchStatusTransfleet <> StatusAfter.vchStatusTransfleet)
   Begin
	  update dbo.Nattsafearkiv set dtmTransportSpecification = NULL
	  from Nattsafearkiv
	  inner join deleted on Nattsafearkiv.intId = deleted.intId
	  where Nattsafearkiv.dtmTransportFetch is NULL --Posen er markert som ikke hentet
   End
End
--If UPDATE(dtmControlledAt)
--Begin
--	--Når opptalt dato settes må det skrives et innslag i tracking som stemmer med opptalt tidspunkt
--	Insert into StatusTracking (intId, intStatus,dtmDate)
--	Select intId,5,isnull(dtmControlledAt, GETDATE())
--	From inserted
--End
