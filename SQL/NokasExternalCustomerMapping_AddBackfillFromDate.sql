USE [BizTalk_Helper]
GO
/****** Nullable, additive column - absent value falls back to the sync's global default backfill
        window. Set per-row when onboarding a customer with a known/desired history depth.
 ******/
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.NokasExternalCustomerMapping')
      AND name = 'BackfillFromDate'
)
BEGIN
    ALTER TABLE [dbo].[NokasExternalCustomerMapping]
    ADD [BackfillFromDate] DATETIME NULL;
END
GO
