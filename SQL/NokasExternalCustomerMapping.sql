USE [Integrations.SN.CMS]
GO

/****** Object:  Table [dbo].[NokasExternalCustomerMapping]    Script Date: 8/8/2026 5:56:52 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[NokasExternalCustomerMapping](
	[RecId] [int] IDENTITY(1,1) NOT NULL,
	[ClientName] [nvarchar](max) NULL,
	[ClientID] [nvarchar](50) NULL,
	[NokasCustomerNumber] [nvarchar](max) NULL,
	[NokasCustomerName] [nvarchar](max) NULL,
	[CustomerRefNumber] [nvarchar](50) NULL,
	[CustomerRefName] [nvarchar](max) NULL,
	[AXProjectId] [nvarchar](50) NULL,
	[Country] [nvarchar](50) NULL,
	[CreatedAt] [datetime] NULL,
	[CreatedBy] [nvarchar](50) NULL,
	[UpdatedAt] [datetime] NULL,
	[UpdatedBy] [nvarchar](50) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO


