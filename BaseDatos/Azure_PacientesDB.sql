/****** Script para Azure SQL Database - PacientesDB ******
  IMPORTANTE: en Azure SQL no se usa CREATE DATABASE ni USE.
  La base se crea desde el portal o con Azure CLI, y este script
  se ejecuta ya conectado a PacientesDB.

  El usuario de conexion es 'adminsql', administrador del servidor
  logico, que tiene acceso a todas las bases. Por eso tampoco
  se crea un login adicional.
**********************************************************/

/****** Object:  Table [dbo].[tbl_paciente] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[tbl_paciente](
	[pac_id] [int] IDENTITY(1,1) NOT NULL,
	[pac_cedula] [varchar](10) NOT NULL,
	[pac_nombre] [varchar](100) NOT NULL,
	[pac_apellido] [varchar](100) NOT NULL,
	[pac_direccion] [varchar](200) NULL,
	[pac_estado] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[pac_id] ASC
)
) ON [PRIMARY]
GO

/****** La cedula es unica: impide registrar dos veces al mismo paciente ******/
ALTER TABLE [dbo].[tbl_paciente] ADD UNIQUE NONCLUSTERED 
(
	[pac_cedula] ASC
)
GO

/****** Todo paciente nuevo nace activo (borrado logico) ******/
ALTER TABLE [dbo].[tbl_paciente] ADD DEFAULT ((1)) FOR [pac_estado]
GO

/****** Datos de prueba ******/
SET IDENTITY_INSERT [dbo].[tbl_paciente] ON 
INSERT [dbo].[tbl_paciente] ([pac_id], [pac_cedula], [pac_nombre], [pac_apellido], [pac_direccion], [pac_estado]) VALUES (1, N'0601234567', N'Ana', N'Morales', N'Av. Daniel León Borja', 1)
INSERT [dbo].[tbl_paciente] ([pac_id], [pac_cedula], [pac_nombre], [pac_apellido], [pac_direccion], [pac_estado]) VALUES (2, N'0609876543', N'Luis', N'Cevallos', NULL, 1)
INSERT [dbo].[tbl_paciente] ([pac_id], [pac_cedula], [pac_nombre], [pac_apellido], [pac_direccion], [pac_estado]) VALUES (3, N'1727411967', N'Martin', N'Barahona', N'Bellavista del Sur', 1)
SET IDENTITY_INSERT [dbo].[tbl_paciente] OFF
GO