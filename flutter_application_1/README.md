# AlunosTech Chamados (Android)

App Flutter para colaboradores abrirem e acompanharem chamados de atendimento interno.

## Fluxo

- Solicitação de cadastro enviada à API e acesso bloqueado até aprovação pela equipe web.
- Login autorizado pela API e perfil básico mantido no SQLite do aparelho.
- Novos chamados gravados localmente antes do envio; chamados pendentes são sincronizados quando a conexão retorna.
- O token de acesso é armazenado com o armazenamento seguro do Android, e a senha não é persistida.
- Respostas e atualizações ficam disponíveis nos detalhes do chamado após sincronização.

O primeiro login requer internet. Se a sessão expirar ou o app for deslogado, conecte-se e entre novamente para sincronizar. Não desinstale o app antes de enviar os chamados pendentes, pois eles estão no armazenamento local do aparelho.

## Executar no emulador

```powershell
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5079
```

Para aparelho físico ou produção, configure `API_BASE_URL` com um endereço HTTPS acessível pelo dispositivo.
Também é possível alterar o endereço pelo botão **Configurar servidor** na tela de login; a preferência fica salva no SQLite local.

## Build release

```powershell
flutter build apk --release --split-per-abi
```

Os APKs ficam em `build/app/outputs/flutter-apk/`. Para a maioria dos aparelhos atuais, use `app-arm64-v8a-release.apk`. Configure a URL real da API no botão **Configurar servidor** (ou via `--dart-define=API_BASE_URL=...`). A assinatura release lê `android/key.properties`, arquivo local ignorado pelo Git; o keystore é compartilhado com o projeto financeiro. Guarde uma cópia segura e mantenha essa mesma chave para futuras atualizações do app.
