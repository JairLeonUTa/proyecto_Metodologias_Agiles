BEGIN TRANSACTION;
GO

ALTER TABLE [ASIGNATURA] ADD [Activa] bit NOT NULL DEFAULT CAST(1 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020190757_AsignaturaActiva', N'8.0.10');
GO

COMMIT;
GO

