USE [Nattsafe]
GO

/****** Object:  Table [dbo].[Nattsafearkiv]    Script Date: 8/8/2026 5:52:57 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Nattsafearkiv](
	[intId] [int] IDENTITY(1,1) NOT NULL,
	[intInnkastnr] [varchar](20) NOT NULL,
	[intBagID] [int] NULL,
	[vchEasysafeaccount] [varchar](11) NOT NULL,
	[vchCustomer_id] [varchar](10) NULL,
	[vchName] [varchar](50) NULL,
	[intLocation_id] [int] NULL,
	[vchLocation] [varchar](50) NULL,
	[vchButikkNr] [varchar](20) NULL,
	[vchKKText] [varchar](20) NULL,
	[dtmRegDate] [smalldatetime] NULL,
	[vchRef] [varchar](10) NULL,
	[dblAmount] [float] NULL,
	[dblSeddel1000] [float] NULL,
	[dblSeddel500] [float] NULL,
	[dblSeddel200] [float] NULL,
	[dblSeddel100] [float] NULL,
	[dblSeddel50] [float] NULL,
	[dblAmountSeddel] [float] NULL,
	[dblAmountMynt] [float] NULL,
	[dblAmountCash] [float] NULL,
	[dblAmountReserve] [float] NULL,
	[dblAmountAnnet] [float] NULL,
	[dblForCurrencies] [float] NULL,
	[vchBoxnumber] [varchar](10) NULL,
	[intPlombnumber] [int] NULL,
	[intStatus] [int] NULL,
	[vchStatusDescription] [varchar](255) NULL,
	[dtmSubmitdate] [smalldatetime] NULL,
	[vchConfirmedStatus] [varchar](50) NULL,
	[vchStatusComments] [varchar](4000) NULL,
	[vchMyReference] [varchar](50) NULL,
	[dtmOmsetningsdato] [smalldatetime] NULL,
	[dtmTransportspecification] [datetime] NULL,
	[bitConfirmed] [bit] NULL,
	[dblControlledAmount] [float] NULL,
	[dtmControlledAt] [smalldatetime] NULL,
	[dtmTransportFetch] [smalldatetime] NULL,
	[SOnumber] [varchar](50) NULL,
	[bitRequireApproval] [bit] NULL,
 CONSTRAINT [PK_Nattsafearkiv] PRIMARY KEY CLUSTERED 
(
	[intId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Nattsafearkiv] ADD  CONSTRAINT [DF_Nattsafearkiv_intStatus]  DEFAULT ((1)) FOR [intStatus]
GO

ALTER TABLE [dbo].[Nattsafearkiv] ADD  CONSTRAINT [DF_Nattsafearkiv_dtmSubmitdate]  DEFAULT (getdate()) FOR [dtmSubmitdate]
GO


