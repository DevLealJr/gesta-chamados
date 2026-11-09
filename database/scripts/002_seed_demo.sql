USE AlunosTechHubDb;
GO
INSERT INTO dbo.Clientes (Id, Nome, Documento, Segmento, Ativo)
VALUES (NEWID(), 'Alunos Tech', '12.345.678/0001-90', 'Educa??o', 1);
GO
