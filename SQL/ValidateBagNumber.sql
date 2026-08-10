USE [NokasMwCacheDbV3]
GO
/****** Object:  StoredProcedure [dbo].[ValidateBagNumberV2]    Script Date: 8/8/2026 7:04:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[ValidateBagNumberV2]
	@bagNumber varchar(20)
AS
-- mwUpdCancelRegistration '1234'
BEGIN
	BEGIN TRY
		
	Declare @errorCode varchar(10)
	Declare @errorMessage varchar(300)
	Declare @bagNumberVa varchar(20)
	
	Set @errorCode = '0'
	Set @errorMessage = ''

	Select @bagNumberVa = BagNumber From dbo.Registration Where BagNumber = @bagNumber
	If (@@ROWCOUNT > 0)
		Begin
			Set @errorCode = '1'
			Set @errorMessage = 'Bag Number already exist in database, please use other bag number'
		End
		
		Select 
			@errorCode AS ErrorNumber,
			@errorMessage AS ErrorMessage;
	        
	END Try
	
BEGIN CATCH
    Select 
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage;
END CATCH;

END
