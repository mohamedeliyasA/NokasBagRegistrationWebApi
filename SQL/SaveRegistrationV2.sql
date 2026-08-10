USE [NokasMwCacheDbV3]
GO
/****** Object:  StoredProcedure [dbo].[SaveRegistrationV2]    Script Date: 8/8/2026 7:02:16 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



ALTER PROCEDURE [dbo].[SaveRegistrationV2] -- '9000022043', '100010', '123', '2014-02-25 00:00:00Z', '62501', 'PauloPullan', '1', '10', '10', '10', '1000', '', null, '100075', '250052', '0', 'paulopullantest@gmail.com', null, '12320160104'
	@bagNumber varchar(20),	
	@customerId varchar(10) = null,
	@referenceStatement nvarchar(100) = null,
	@turnOverDate varchar(20) = null,
	@totalAmount varchar(50) = null,
	@name nvarchar(200) = null,
	@seddel1000 varchar(20) = null,
	@seddel500 varchar(20) = null,
	@seddel200 varchar(20) = null,
	@seddel100 varchar(20) = null,
	@seddel50 varchar(20) = null,
	@countedAmount varchar(50) = null,
	@registeredCash varchar(50) = null,
	@registeredCoins varchar(50) = null,
	@registeredChecks varchar(50) = null,
	@registeredForeignCurrency varchar(50) = null,
	@userName nvarchar(50) = null,
	@dateOfSale varchar(20) = null, 
	@easySafeAccount varchar(11) = null,
	@locationId varchar(10) =null,
	@registrationApproval varchar(5)=null,
	@containsValuta varchar(5)=null,
	@isBankAccount varchar(5)=null,
	@shopNumber nvarchar(20)=null,
	@soNumber nvarchar(50)=null,
	@dateDelivered varchar(20) = null,
	@registrationType varchar(50) = null,
	@actionFlag varchar(50) = null,
	@registrationSubType varchar(200) = null,
	@downStreamRegistrationId int = null,
	@registrationStatus int = null,
	@country nvarchar(50),
	@voucherNumber nvarchar(100) = null,
	@voucherAmount nvarchar(100) = null,
	@confirmFlag nvarchar(1) = null,
	@userId nvarchar(200) = null,
	@registrationForeignCurrency as dbo.RegistrationForeignCurrency readonly,
	@registrationVouchers as dbo.RegistrationVouchers readonly
AS
BEGIN



BEGIN TRY


Declare @bagNumberVa varchar(20)
Declare @errorCode varchar(10)
Declare @errorMessage varchar(300)
Declare @intRegRecId bigint
Declare @statusId int = 1
Declare @CacheDBRegNumber INT
Declare @seddel50int INT, @seddel100int INT, @seddel200int  INT, @seddel500int  INT, @seddel1000int INT

Set @errorCode = '0'
Set @errorMessage = ''

Set @customerId = RTRIM(LTRIM(@customerId))
Set @turnOverDate = REPLACE(@turnOverDate, 'Z', '')
Set @dateOfSale = REPLACE(@turnOverDate, 'Z', '')

Set @dateDelivered = REPLACE(@dateDelivered, 'Z', '')

-- Use the registration sub type only when this customer HQ has been customization
if ((select COUNT(*) from RegistrationSubTypeCustomization r, vHQAndBranches h where r.CustomerHQRecId = h.HqRecordId and h.CustomerNumber = @customerId) = 0)
Begin
	Set @registrationSubType = null
End

if (@actionFlag = 'ValutaUpdate')
Begin
	Update Registration Set NattsafeRecordId = @downStreamRegistrationId, RegistrationStatusRecId = @registrationStatus Where CacheDBRegNumber = @bagNumber
	Set @intRegRecId = @downStreamRegistrationId
End
Else if (@actionFlag = 'DownStreamSystemStatus')
Begin
	Update Registration Set NattsafeRecordId = @downStreamRegistrationId, RegistrationStatusRecId = @registrationStatus Where CacheDBRegNumber = @bagNumber
	Set @intRegRecId = @downStreamRegistrationId
End
Else if (@actionFlag = 'PortalDelete')
Begin
	Update Registration Set RegistrationStatusRecId = @registrationStatus Where CacheDBRegNumber = @downStreamRegistrationId
	Set @intRegRecId = @downStreamRegistrationId
End
Else if (@actionFlag = 'PortalUpdate')
Begin
	set @intRegRecId = (select RecId from Registration where [BagNumber] = @bagNumber and [CustomerRecId] = (Select RecId From vHQAndBranches Where CustomerNumber = @customerId))
	
	UPDATE [NokasMwCacheDbV3].[dbo].[Registration]
	   SET [ReferenceStatement] = @referenceStatement
		  ,[TurnOverDate] = @turnOverDate
		  ,[RegisteredName] = @name
		  ,[RegisteredBy] = @userName
		  ,[TotalAmount] = CAST(@totalAmount as Bigint)/100.00
		  ,[RegisteredCoins] = CAST(@registeredCoins as Bigint)/100.00
		  ,[RegisteredCash] = (CAST(@registeredCoins as Bigint) + CAST(@registeredCash as Bigint))/100.00 
		  ,[RegisteredChecks] = CAST(@registeredChecks as Bigint)/100.00
		  ,[RegisteredForeignCurrency] = CAST(@registeredForeignCurrency as Bigint)/100.00 
		  ,[ShopNumber] = @shopNumber
		  ,[SONumber] = @soNumber
		  --,[RegistrationStatusRecId] = 
		  ,[LastUpdateDate] = GETDATE()
		  ,[LastUpdateBy] = 'ESB'
		  ,[ContainsValuta] = @containsValuta
		  ,[Denom50] = @seddel50
		  ,[Denom100] = @seddel100
		  ,[Denom200] = @seddel200
		  ,[Denom500] = @seddel500
		  ,[Denom1000] = @seddel1000
		  ,[BankAccount] = @easySafeAccount
		  ,[RegistrationSubType] = @registrationSubType
		  ,[Country] = @Country
		  ,[ConfirmFlag] = @ConfirmFlag
		  ,[ConfirmDateTime] = GETDATE()
		  ,[RegisteredUserId] = @UserId
	 WHERE	[RecId] = @intRegRecId
	 
	-- Delete all the foreign currencies and create them again based on the new information
	DELETE from RegistrationForeignCurrency where RegistrationRecId = @intRegRecId
	Insert into dbo.RegistrationForeignCurrency(RegistrationRecId
				, CurrencyISOCode
				, BuyNotes
				, NotesAmountNOK
				, NotesAmountCurrency
				, BuyCheques
				, ChequeAmountNOK
				, ChequeAmountCurrency
				, CurrencyAmount
				, LastUpdateDate
				, LastUpdateBy	
				, ItemId
				, ExchangeRate			
			)
			(Select @intRegRecId					
				, fc.CurrencyISOCode
				, fc.BuyNotes
				, fc.NotesAmountNok
				, fc.NotesAmountCurrency
				, fc.BuyCheques
				, fc.ChequesAmountNOK
				, fc.ChequesAmountCurrency
				, fc.CurrencyAmount
				, GETDATE()
				, 'ESB'
				, (Select s.RecId From OrderableItems s Where s.ItemNumber = fc.ItemId and s.ItemTypeId IN (3,4)) --Valuta Items
				, ExchangeRate from @registrationForeignCurrency fc
				where fc.CurrencyISOCode is not null)
				
	-- Delete all the foreign currencies and create them again based on the new information
	DELETE from [RegistrationVouchers] where RegistrationRecId = @intRegRecId				

	INSERT INTO [RegistrationVouchers]
			   ([RegistrationRecId]
			   ,[VoucherNumber]
			   ,[VoucherAmount]
			   ,[VoucherCurrencyCode]
			   ,[LocalCurrencyCode]
			   ,[ExchangeRate]
			   ,[BankBranchNumber]
			   ,[BankBranchName]
			   ,[LastUpdatedBy]
			   ,[LastUpdatedDate]
			   )
			   (Select @intRegRecId
			   ,[VoucherNumber]
			   ,[VoucherAmount]
			   ,[VoucherCurrencyCode]
			   ,[LocalCurrencyCode]
			   ,[ExchangeRate]
			   ,[BankBranchNumber]
			   ,[BankBranchName]
			   ,'ESB'
			   ,GETDATE()
			   from @registrationVouchers rv
			   where rv.VoucherAmount is not null)

End
Else

Begin

Select @bagNumberVa = 1 From dbo.Registration Where BagNumber = @bagNumber

If (@@ROWCOUNT > 0 and @registrationType != 'Valuta')
	Begin
		
		Set @errorCode = '1'
		Set @errorMessage = 'Bag Number already exist in database, please use other bag number'
	End

If (@errorCode  = '0')
	Begin
		if (@registrationType = 'Valuta')
		Begin
			-- Get the ValutaOrderSequence number
   			EXEC [dbo].[GetValutaSequenceNextVal] @CacheDBRegNumber OUTPUT
		End
		
		Insert into dbo.Registration (BagNumber				
				, CustomerRecId
				, ReferenceStatement
				, TurnOverDate
				, RegistrationDate
				, TotalAmount
				, RegisteredName
				, RegisteredBy
				, CountedAmount
				, RegisteredCoins
				, RegisteredCash
				, RegisteredChecks
				, RegisteredForeignCurrency
				, DateOfSale
				, ShopNumber
				, SONumber
				, RegistrationStatusRecId
				, LastUpdateDate
				, LastUpdateBy
				, PickedUpDate	
				, ContainsValuta	
				, Denom50
				, Denom100
				, Denom200
				, Denom500
				, Denom1000	
				, BankAccount
				, CreatedBy
				, CreatedAt
				, RegistrationType
				, CacheDBRegNumber
				, RegistrationSubType
				, Country
				, RegisteredUserId
				, ConfirmFlag
				, ConfirmDateTime
		)
		Values( 
				@bagNumber
				, (Select RecId From vHQAndBranches Where CustomerNumber = @customerId)
				, @referenceStatement
				, @turnOverDate				
				, GETDATE()
				,  CAST(@totalAmount as Bigint)/100.00 
				, case when @name = '' then null else @name end
				, @userName
				, @countedAmount
				, CAST(@registeredCoins as Bigint)/100.00
				,	
					(CAST(@registeredCoins as Bigint) + CAST(@registeredCash as Bigint))/100.00 
				,  CAST(@registeredChecks as Bigint)/100.00  
				, CAST(@registeredForeignCurrency as Bigint)/100.00 
				, @dateOfSale
				, case when @shopNumber = '' then null else @shopNumber end
				, case when @soNumber = '' then null else @soNumber end
				, @registrationStatus
				, GETDATE()
				, 'ESB'
				, @dateDelivered
				, @containsValuta
				, case when @seddel50 = '' then null else @seddel50 end
				, case when @seddel100 = '' then null else @seddel100 end
				, case when @seddel200 = '' then null else @seddel200 end
				, case when @seddel500 = '' then null else @seddel500 end
				, case when @seddel1000 = '' then null else @seddel1000 end
				--, CAST (@seddel50int as nvarchar(20))
				--, CAST (@seddel100int as varchar(20))
				--, CAST (@seddel200int as varchar(20))
				--, CAST (@seddel500int as varchar(20))
				--, CAST (@seddel1000int as varchar(20))
				, @easySafeAccount
				, 'CashPortal'
				, GETDATE()
				, case when @registrationType = '' then null else @registrationType end
				, @CacheDBRegNumber
				, @registrationSubType
				, @Country
				, @UserId
				, @ConfirmFlag
				, GETDATE()
		)
		Set @intRegRecId = @@IDENTITY
		
			--- Denomination
			
			--Insert into dbo.RegistrationDenomination (RegistrationRecId,
			--	Denomination, Amount, LastUpdateDate, LastUpdateBy)				
			--Select @@IDENTITY, 'Seddel1000', CAST(@seddel1000 as bigint), GETDATE(), 'ESB'
			--union all Select @@IDENTITY, 'Seddel500', CAST(@seddel500 as bigint), GETDATE(), 'ESB'
			--union all Select @@IDENTITY, 'Seddel200', CAST(@seddel200 as bigint), GETDATE(), 'ESB'
			--union all Select @@IDENTITY, 'Seddel100', CAST(@seddel100 as bigint), GETDATE(), 'ESB'
			--union all Select @@IDENTITY, 'Seddel50', CAST(@seddel50 as bigint), GETDATE(), 'ESB'
		
			----	Foreign Currencies
	
		Insert into dbo.RegistrationForeignCurrency(RegistrationRecId
				
				, CurrencyISOCode
				, BuyNotes
				, NotesAmountNOK
				, NotesAmountCurrency
				, BuyCheques
				, ChequeAmountNOK
				, ChequeAmountCurrency
				, CurrencyAmount
				, LastUpdateDate
				, LastUpdateBy	
				, ItemId
				, ExchangeRate			
			)
			(Select @intRegRecId					
				, fc.CurrencyISOCode
				, fc.BuyNotes
				, fc.NotesAmountNok
				, fc.NotesAmountCurrency
				, fc.BuyCheques
				, fc.ChequesAmountNOK
				, fc.ChequesAmountCurrency
				, fc.CurrencyAmount
				, GETDATE()
				, 'ESB'
				, (Select s.RecId From OrderableItems s Where s.ItemNumber = fc.ItemId and s.ItemTypeId IN (3,4)) --Valuta Items
				, ExchangeRate from @registrationForeignCurrency fc
				where fc.CurrencyISOCode is not null)

		--Save the voucher details for Denmark bags
		INSERT INTO [NokasMwCacheDbV3].[dbo].[RegistrationVouchers]
				   ([RegistrationRecId]
				   ,[VoucherNumber]
				   ,[VoucherAmount]
				   ,[VoucherCurrencyCode]
				   ,[LocalCurrencyCode]
				   ,[ExchangeRate]
				   ,[BankBranchNumber]
				   ,[BankBranchName]
				   ,[LastUpdatedBy]
				   ,[LastUpdatedDate]
				   )
				   (select 
					@intRegRecId
				   ,[VoucherNumber]
				   ,[VoucherAmount]
				   ,[VoucherCurrencyCode]
				   ,[LocalCurrencyCode]
				   ,[ExchangeRate]
				   ,[BankBranchNumber]
				   ,[BankBranchName]
				   ,'ESB'
				   ,GETDATE()
				   from @registrationVouchers rv
			   where rv.VoucherAmount is not null)
				
		
		if (@registrationType = 'Valuta')
		Begin
			-- Return the intermediate ValutaOrderSequence number generated
   			Set @intRegRecId = @CacheDBRegNumber
		End

	End

End
END TRY

BEGIN CATCH
     
        Set @errorCode = ERROR_NUMBER() 
        Set @errorMessage =  ERROR_MESSAGE();
        
END CATCH;


Select 
		@errorCode AS ErrorNumber,
        @errorMessage AS ErrorMessage,
        @intRegRecId as RegistrationRecId;
        
END
