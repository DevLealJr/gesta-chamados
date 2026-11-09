# Arquitetura

- **Domain:** entidades do sistema, estados e prioridades de chamados.
- **Application:** contratos de serviço e DTOs compartilhados.
- **Infrastructure:** Entity Framework Core, SQL Server, ASP.NET Identity e emissão de JWT.
- **API:** autenticação, cadastro pendente, aprovação de colaboradores, setores e chamados.
- **Web:** portal ASP.NET MVC para responsáveis aprovarem cadastros, manter setores e responder/atualizar chamados.
- **Mobile:** app Flutter para colaboradores. Mantém perfil e fila de chamados em SQLite; guarda o token com o armazenamento seguro da plataforma.

## Sincronização offline

O app grava um chamado local com identificador UUID antes de tentar enviá-lo à API. A API usa esse mesmo identificador para reconhecer uma repetição de envio sem criar um segundo chamado. Chamados sem envio concluído permanecem na fila local e são tentados novamente ao atualizar ou reabrir a lista.

O primeiro login e o envio do cadastro dependem de conexão. O uso offline pressupõe uma sessão autenticada previamente no aparelho. A API central usa SQL Server; SQLite é usado no dispositivo Android.
