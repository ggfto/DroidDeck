# Arquivos e configurações

## Dados no PC

**`%AppData%\DroidDeck\`** (Roaming) — seus decks:

| Arquivo | Conteúdo |
|---|---|
| `Profiles\<id>.json` | Um arquivo por perfil: nome e botões. Ícones personalizados ficam embutidos em base64. |
| `layout.json` | Tamanho da grade informado pelo celular (`rows`, `columns`). |

**`%LocalAppData%\DroidDeck\`** — chaves e integrações:

| Arquivo | Conteúdo |
|---|---|
| `apikey` | A chave de pareamento. Apague-a para revogar todos os celulares pareados. |
| `discord.json` | Client ID, Client Secret e tokens do Discord. |
| `obs.json` | Host, porta e senha do OBS. A presença dele ativa a conexão automática com o OBS. |
| `soundboard.json` | Dispositivos de saída e volume do soundboard. |
| `SoundCache\*.mp3` | Sons baixados do MyInstants. Pode apagar sem problema. |
| `tuya.json` | Código de usuário da Tuya, token de sessão e registro do app (`ClientId`, `Schema`). |

**`%LocalAppData%\DroidDeck.log`** — o arquivo de log.

!!! info "Os segredos são cifrados"
    `apikey`, `discord.json`, `obs.json` e `tuya.json` são cifrados com a DPAPI do Windows e só podem
    ser lidos pela sua conta do Windows neste PC. Arquivos de versões antigas, em texto puro, são
    cifrados automaticamente na primeira leitura. Para editar um deles à mão (ex.: o `ClientId` da
    Tuya), salve-o como JSON puro; o DroidDeck cifra de novo na próxima inicialização.

## Backup e troca de PC

Copie `%AppData%\DroidDeck\Profiles\` para guardar seus decks. Os segredos ficam presos a este PC e
à sua conta do Windows, então num PC novo você configura as integrações e pareia os celulares de novo.

## Opções de linha de comando

| Opção | Efeito |
|---|---|
| *(nenhuma)* | App na bandeja + servidor. |
| `--headless` / `--no-tray` | Só o servidor, sem ícone na bandeja. Pare com Ctrl+C. |
| `--print-pairing` | Imprime a URI de pareamento, salva o QR em `%TEMP%\droiddeck-pair.png` e sai. |

| Variável de ambiente | Efeito |
|---|---|
| `ASPNETCORE_URLS` | Muda o endereço em que o servidor escuta (padrão `http://0.0.0.0:4787`). O QR code e o redirect do Discord continuam usando a porta 4787. |
| `ASPNETCORE_ENVIRONMENT=Development` | Ativa o Swagger em `/swagger` e páginas de erro detalhadas. |

## `appsettings.json`

Fica ao lado do `DroidDeck.exe`:

| Chave | Padrão | Significado |
|---|---|---|
| `EnableDiscovery` | `true` | Responde a pedidos de descoberta UDP na porta 7573. |
| `EnableSoftwareActivation` | `false` | Ativa o endpoint legado `/api/Software/activate`. |
| `AllowedTargets` | *(vazio)* | Nomes de processo, separados por vírgula, permitidos pelos endpoints legados de mudo `/api/Software`. |
