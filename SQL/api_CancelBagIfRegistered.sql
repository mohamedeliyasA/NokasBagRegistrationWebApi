USE [Nattsafe]
GO
/****** Object:  StoredProcedure [dbo].[api_CancelBagIfRegistered]    Script Date: 8/8/2026 10:11:10 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[api_CancelBagIfRegistered]
    @BagNumber    VARCHAR(50),
    @OutStatusCode INT OUTPUT  -- 0 = success, 1 = Not found, 2 = already cancelled, 3 = invalid status, 4 = error
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
	

	 DECLARE @currentStatus INT;
	 DECLARE @transportFetch smalldatetime;
        SELECT TOP (1) @currentStatus = N.intStatus, @transportFetch = dtmTransportFetch
        FROM dbo.Nattsafearkiv N (nolock)
        WHERE N.intInnkastnr = @BagNumber
        ORDER BY N.dtmSubmitDate DESC;
       
        DECLARE @UpdatedRows TABLE
        (
            intInnkastnr VARCHAR(50),
            vchCustomer_id VARCHAR(50),
            dblAmount DECIMAL(18,2) NULL,
			vchLocation VARCHAR(50),
            dtmRegDate DATETIME NULL,
			dtmTransportFetch SMALLDATETIME NULL
		
        );

		-- Perform the update to cancel(10) when the status is 0,1,2
        UPDATE dbo.Nattsafearkiv
        SET intStatus = 10,
            vchStatusDescription = 'Kansellert'
        OUTPUT
            inserted.intInnkastnr,
            inserted.vchCustomer_id,
            inserted.dblAmount,
			inserted.vchLocation,
            inserted.dtmRegDate,
			inserted.dtmTransportFetch
			
        INTO @UpdatedRows
        WHERE intInnkastnr = @BagNumber
          AND intStatus in (0, 1,2)
		  And dtmTransportFetch is NULL;  -- only update if currently registered and not fetched by Guards in ServiceNow

        IF EXISTS (SELECT 1 FROM @UpdatedRows)
        BEGIN
            -- Success: return the updated row and status 0
            SET @OutStatusCode = 0;

            SELECT TOP (1)
                U.intInnkastnr,
                U.vchCustomer_id,
                C.vchName,
                U.dblAmount,
				U.vchLocation,
                U.dtmRegDate,
                @currentStatus as intStatus,
				U.dtmTransportFetch
            FROM @UpdatedRows AS U
            LEFT JOIN dbo.Customer (nolock) AS C ON U.vchCustomer_id = C.vchCustomer_id;

            RETURN;
        END

 
      
       

        IF @currentStatus IS NULL
        BEGIN
            SET @OutStatusCode = 1;  -- not found
            RETURN;                  -- no resultset for code 1
        END

        -- If already cancelled -> code 2, return the current row
        IF @currentStatus = 10
        BEGIN
            SET @OutStatusCode = 2;

            SELECT TOP (1)
                N.intInnkastnr,
                N.vchCustomer_id,
                C.vchName,
                N.dblAmount,
				N.vchLocation,
                N.dtmRegDate,
                @currentStatus as intStatus,
				N.dtmTransportFetch
            FROM dbo.Nattsafearkiv (nolock) AS N
            INNER JOIN dbo.Customer(nolock) AS C ON N.vchCustomer_id = C.vchCustomer_id
            WHERE N.intInnkastnr = @BagNumber
            ORDER BY N.dtmSubmitDate DESC;

            RETURN;
        END

        -- Any other non- 0, 1 ,2 status -> code 3, return the current row
        SET @OutStatusCode = 3;

        SELECT TOP (1)
            N.intInnkastnr,
            N.vchCustomer_id,
            C.vchName,
            N.dblAmount,
			N.vchLocation,
            N.dtmRegDate,
            @currentStatus as intStatus,
			N.dtmTransportFetch
        FROM dbo.Nattsafearkiv (nolock) AS N
        INNER JOIN dbo.Customer(nolock) AS C ON N.vchCustomer_id = C.vchCustomer_id
        WHERE N.intInnkastnr = @BagNumber
        ORDER BY N.dtmSubmitDate DESC;

        RETURN;
    END TRY
    BEGIN CATCH
        SET @OutStatusCode = 4;
        RETURN;
    END CATCH
END
