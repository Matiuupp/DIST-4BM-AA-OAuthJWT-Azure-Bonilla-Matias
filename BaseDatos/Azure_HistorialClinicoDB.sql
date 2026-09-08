/****** Script para Azure SQL Database - HistorialDB ******
  IMPORTANTE: en Azure SQL no se usa CREATE DATABASE ni USE.
  La base se crea desde el portal o con Azure CLI, y este script
  se ejecuta ya conectado a HistorialDB.

  Nota de arquitectura: hist_paciente_id NO tiene FOREIGN KEY.
  La tabla tbl_paciente vive en otra base de datos (PacientesDB) y
  SQL Server no permite llaves foraneas entre bases distintas.
  La relacion 1:N se mantiene desde la logica de la aplicacion.
**********************************************************/

/****** Object:  Table [dbo].[tbl_historialclinico] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[tbl_historialclinico](
	[hist_id] [int] IDENTITY(1,1) NOT NULL,
	[hist_paciente_id] [int] NOT NULL,
	[hist_numero] [varchar](20) NOT NULL,
	[hist_diagnostico] [varchar](500) NOT NULL,
	[hist_tratamiento] [varchar](500) NULL,
	[hist_fecha] [datetime] NOT NULL,
	[hist_estado] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[hist_id] ASC
)
) ON [PRIMARY]
GO

/****** El numero de historia clinica es unico ******/
ALTER TABLE [dbo].[tbl_historialclinico] ADD UNIQUE NONCLUSTERED 
(
	[hist_numero] ASC
)
GO

/****** Si no se envia fecha, se pone la actual ******/
ALTER TABLE [dbo].[tbl_historialclinico] ADD DEFAULT (getdate()) FOR [hist_fecha]
GO

/****** Todo historial nuevo nace activo (borrado logico) ******/
ALTER TABLE [dbo].[tbl_historialclinico] ADD DEFAULT ((1)) FOR [hist_estado]
GO

/****** Datos de prueba ******/
SET IDENTITY_INSERT [dbo].[tbl_historialclinico] ON 
INSERT [dbo].[tbl_historialclinico] ([hist_id], [hist_paciente_id], [hist_numero], [hist_diagnostico], [hist_tratamiento], [hist_fecha], [hist_estado]) VALUES (1, 1, N'HC-0001', N'Faringitis aguda', N'Amoxicilina 500mg cada 8 horas por 7 dias', GETDATE(), 1)
INSERT [dbo].[tbl_historialclinico] ([hist_id], [hist_paciente_id], [hist_numero], [hist_diagnostico], [hist_tratamiento], [hist_fecha], [hist_estado]) VALUES (2, 1, N'HC-0002', N'Control post tratamiento', NULL, GETDATE(), 1)
INSERT [dbo].[tbl_historialclinico] ([hist_id], [hist_paciente_id], [hist_numero], [hist_diagnostico], [hist_tratamiento], [hist_fecha], [hist_estado]) VALUES (3, 2, N'HC-0003', N'Gastritis', N'Omeprazol 20mg en ayunas', GETDATE(), 1)
SET IDENTITY_INSERT [dbo].[tbl_historialclinico] OFF
GO