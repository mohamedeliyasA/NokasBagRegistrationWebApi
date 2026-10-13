USE [Nattsafe]
GO
/****** Object:  StoredProcedure [dbo].[api_CountingResultFetchChanges]
        Read-only delta/backfill feed for the counting-result sync: returns changed or backfilled
        Nattsafearkiv rows plus their compact status history, for a set of customer numbers.
 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

DROP PROCEDURE IF EXISTS [dbo].[usp_CountingResult_FetchChanges];
GO

CREATE OR ALTER PROCEDURE [dbo].[api_CountingResultFetchChanges]
    @Mode            VARCHAR(10),        -- 'Delta' or 'Backfill'
    @SinceDate       DATETIME2,          -- Delta: StatusTracking watermark minus overlap. Backfill: BackfillFromDate (or global default).
    @CustomerNumbers NVARCHAR(MAX) = NULL, -- comma-separated vchCustomer_id list. NULL/empty = no customer filter.
    @PageSize        INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    IF @Mode NOT IN ('Delta', 'Backfill')
    BEGIN
        RAISERROR('Invalid @Mode. Expected ''Delta'' or ''Backfill''.', 16, 1);
        RETURN;
    END

    -- XML-based split (works on older compatibility levels than STRING_SPLIT requires).
    DECLARE @CustomersXml XML = CASE
        WHEN @CustomerNumbers IS NULL OR LTRIM(RTRIM(@CustomerNumbers)) = '' THEN NULL
        ELSE CAST('<c>' + REPLACE(@CustomerNumbers, ',', '</c><c>') + '</c>' AS XML)
    END;

    DECLARE @Customers TABLE (CustomerNumber VARCHAR(10) PRIMARY KEY);
    INSERT INTO @Customers (CustomerNumber)
    SELECT DISTINCT v.CustomerNumber
    FROM (
        SELECT DISTINCT LTRIM(RTRIM(x.value('.', 'VARCHAR(10)'))) AS CustomerNumber
        FROM @CustomersXml.nodes('/c') AS tbl(x)
    ) v
    WHERE @CustomersXml IS NOT NULL AND v.CustomerNumber <> '';

    DECLARE @HasCustomerFilter BIT = CASE WHEN EXISTS (SELECT 1 FROM @Customers) THEN 1 ELSE 0 END;

    -- Candidate intIds for this run, with the "changed at" timestamp used both for ordering/paging
    -- and as the document's lastUpdatedAt (falls back to dtmRegDate when no StatusTracking row exists,
    -- e.g. rows older than the trigger, or a first-time backfill page before any status change).
    DECLARE @Candidates TABLE
    (
        intId         INT PRIMARY KEY,
        LastUpdatedAt DATETIME2 NOT NULL
    );

    IF @Mode = 'Delta'
    BEGIN
        -- StatusTracking is a complete change signal for Nattsafearkiv: the trigger fires on every
        -- intStatus change, and counted-amount corrections always change intStatus too.
        INSERT INTO @Candidates (intId, LastUpdatedAt)
        SELECT ST.intId, MAX(ST.dtmDate)
        FROM dbo.StatusTracking ST WITH (NOLOCK)
        WHERE ST.dtmDate > @SinceDate
        GROUP BY ST.intId;
    END
    ELSE -- Backfill: full history for the given customer(s) from BackfillFromDate, StatusTracking optional
    BEGIN
        INSERT INTO @Candidates (intId, LastUpdatedAt)
        SELECT N.intId, COALESCE(MAX(ST.dtmDate), N.dtmRegDate, N.dtmSubmitdate)
        FROM dbo.Nattsafearkiv N WITH (NOLOCK)
        LEFT JOIN dbo.StatusTracking ST WITH (NOLOCK) ON ST.intId = N.intId
        WHERE N.dtmRegDate >= @SinceDate
          AND (@HasCustomerFilter = 0 OR N.vchCustomer_id IN (SELECT CustomerNumber FROM @Customers))
        GROUP BY N.intId, N.dtmRegDate, N.dtmSubmitdate;
    END

    -- Main result set: one row per changed/backfilled bag, current Nattsafearkiv state.
    SELECT TOP (@PageSize)
        N.intId                AS NattsafeRecordId,
        N.intInnkastnr          AS BagNumber,
        N.vchCustomer_id        AS CustomerNumber,
        N.vchMyReference        AS MyReference,
        N.dblAmount             AS DeclaredAmount,
        N.dblControlledAmount   AS CountedAmount,
        N.dtmControlledAt       AS CountedAt,
        N.dtmTransportFetch     AS TransportFetchedAt,
        N.dtmRegDate            AS RegisteredAt,
        N.intStatus             AS StatusCode,
        CA.LastUpdatedAt        AS LastUpdatedAt
    INTO #Page
    FROM @Candidates CA
    INNER JOIN dbo.Nattsafearkiv N WITH (NOLOCK) ON N.intId = CA.intId
    WHERE @HasCustomerFilter = 0
       OR N.vchCustomer_id IN (SELECT CustomerNumber FROM @Customers)
    ORDER BY CA.LastUpdatedAt ASC, N.intId ASC;

    SELECT * FROM #Page ORDER BY LastUpdatedAt ASC, NattsafeRecordId ASC;

    -- Second result set: compact status history for exactly the bags returned above.
    SELECT
        ST.intId    AS NattsafeRecordId,
        ST.intStatus AS StatusCode,
        ST.dtmDate  AS ChangedAt
    FROM dbo.StatusTracking ST WITH (NOLOCK)
    INNER JOIN #Page P ON P.NattsafeRecordId = ST.intId
    ORDER BY ST.intId ASC, ST.dtmDate ASC;

    DROP TABLE #Page;
END
GO
