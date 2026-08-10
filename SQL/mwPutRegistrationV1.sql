USE [Nattsafe]
GO
/****** Object:  StoredProcedure [dbo].[mwPutRegistrationV1]    Script Date: 8/8/2026 7:49:43 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


ALTER PROCEDURE [dbo].[mwPutRegistrationV1]  --'1020334532', '100010', '123', '2014-02-25 00:00:00Z', '62501,27', 'PauloPullan', '1', '10', '10', '10', '1000', '', '59000', '100075', '250052', '0', 'paulopullantest@gmail.com', ''
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
	@isBankAccount varchar(5)=null,
	@shopNumber nvarchar(20)=null,
	@soNumber nvarchar(50)=null,
	@registrationForeignCurrency as dbo.RegistrationForeignCurrency readonly
AS
BEGIN



BEGIN TRY


Declare @bagNumberVa varchar(20)
Declare @errorCode varchar(10)
Declare @errorMessage varchar(300)
Declare @intLocationId int
Declare @location varchar(50)
Declare @boxNumber varchar(50)
Declare @nattsafeRecId bigint

Set @errorCode = '0'
Set @errorMessage = ''

Set @customerId = RTRIM(LTRIM(@customerId))
Set @turnOverDate = REPLACE(@turnOverDate, 'Z', '')
Set @dateOfSale = REPLACE(@turnOverDate, 'Z', '')

Set @intLocationId = (Select intLocation_id From Customer Where vchCustomer_id = @customerId)
Set @location = (Select vchLocation From Location Where intLocation_id = @intLocationId)
Set @boxNumber = (Select vchBoxNumber From Customer Where vchCustomer_id = @customerId)
--Validation

Select @bagNumberVa = intInnkastnr From dbo.Nattsafearkiv Where intInnkastnr = @bagNumber
If (@@ROWCOUNT > 0)
	Begin
		Set @errorCode = '1'
		Set @errorMessage = 'Bag Number already exist in database, please use other bag number'
	End

If (@errorCode  = '0')
	Begin
		Insert into dbo.Nattsafearkiv (
				intInnkastnr
				, vchEasysafeaccount
				, vchCustomer_id
				, intLocation_id
				, vchLocation
				, vchKKText
				, dtmRegDate
				, vchMyReference
				, dblAmount
				, dtmSubmitdate
				, intStatus
				, vchStatusDescription
				, vchName
				, dblSeddel1000
				, dblSeddel500
				, dblSeddel200
				, dblSeddel100
				, dblSeddel50
				, dblAmountSeddel
				, dblAmountMynt
				, dblAmountCash
				, dblAmountReserve
				, dblForCurrencies
				, dtmOmsetningsdato
				, SOnumber
				, vchBoxnumber
				, vchButikkNr
				, bitRequireApproval
		)
		Values( 
				@bagNumber
				--,(Select isnull(vchEasysafeaccount, '') from Customer where vchCustomer_id = @customerId) 
				,case when (Select isnull(vchEasysafeaccount, '') from Customer where vchCustomer_id = @customerId) is not null then (Select isnull(vchEasysafeaccount, '') from Customer where vchCustomer_id = @customerId) else '' end
				, @customerId
				, @intLocationId
				, @location
				, @referenceStatement
				, GETDATE()
				, @userName
				, CASE WHEN ISNULL(@totalAmount, 0) = 0 THEN 0 ELSE CAST(@totalAmount as float)/100.00 END
				, GETDATE()
				, 1
				, 'anbefalet'
				, @name	
				, @seddel1000
				, @seddel500
				, @seddel200
				, @seddel100
				, @seddel50
				, @countedAmount
				, CASE WHEN ISNULL(@registeredCoins, 0) = 0 THEN 0 ELSE CAST(@registeredCoins as float)/100.00 END
				, CASE WHEN ISNULL(CAST(@registeredCoins as float) + CAST(@registeredCash as float), 0) = 0 THEN 0 ELSE	
					(CAST(@registeredCoins as float) + CAST(@registeredCash as float))/100.00 END
				, CASE WHEN ISNULL(@registeredChecks, 0) = 0 THEN 0 ELSE CAST(@registeredChecks as float)/100.00 END
				, CASE WHEN ISNULL(@registeredForeignCurrency, 0) = 0 THEN 0 ELSE CAST(@registeredForeignCurrency as float)/100.00 END
				, @turnOverDate
				, @soNumber
				, @boxNumber
				, (Select vchShopnumber from Customer where vchCustomer_id = @customerId)
				, @registrationApproval

		)
		
		Set @nattsafeRecId = @@IDENTITY
		--  select * from Nattsafearkiv where intInnkastnr = '9000022038'
		
	--Foreign Currencies
	
		Insert into dbo.NattsafeValuta(
				intInnkastnr
				, vchEasysafeaccount
				, vchCustomer_id
				, vchButikkNr
				, CurrencyISOCode
				, BuyNotes
				, NotesAmountNOK
				, NotesAmountCurrency
				, BuyCheques
				, ChequesAmountNOK
				, ChequesAmountCurrency
				, CurrencyAmount
				, dtmRegDate
			)
			(Select 
				@bagNumber
				, @easySafeAccount
				, @customerId
				, @shopNumber
				, CurrencyISOCode
				, BuyNotes
				, NotesAmountNok
				, NotesAmountCurrency
				, BuyCheques
				, ChequesAmountNOK
				, ChequesAmountCurrency
				, CurrencyAmount
				, REPLACE(RegDate, 'Z', '') from @registrationForeignCurrency)
	End
	

END TRY

BEGIN CATCH
     
        Set @errorCode = ERROR_NUMBER()
		Set @errorMessage = ERROR_MESSAGE()
			
END CATCH;

Select 
		@errorCode AS ErrorNumber,
        @errorMessage AS ErrorMessage,
        @nattsafeRecId AS NattsafeRecordId;

END



