# Instalação

O DroidDeck tem duas partes: o **app para Windows** (servidor) e o **app Android** (o seu deck). Os
dois vêm anexados em toda [release no GitHub](https://github.com/ggfto/DroidDeck/releases/latest).

## Windows

1. Baixe o **`DroidDeck-win-x64.zip`** da release mais recente.
2. Extraia em uma pasta definitiva, por exemplo `C:\Program Files\DroidDeck` ou `%LocalAppData%\DroidDeck-app`.
3. Execute o **`DroidDeck.exe`**.

O app não tem janela. Ele fica na **bandeja do sistema**, e uma notificação confirma que está rodando
em `http://localhost:4787/`. O build é autocontido, então você não precisa instalar o .NET.

!!! warning "Windows SmartScreen"
    O executável ainda não tem assinatura de código, então o Windows pode mostrar *"O Windows protegeu
    o computador"*. Clique em **Mais informações → Executar assim mesmo**.

### Menu da bandeja

Clique com o botão direito no ícone da bandeja:

| Item | O que faz |
|---|---|
| **Abrir no navegador** | Abre o configurador em `http://localhost:4787/` (o clique duplo no ícone faz o mesmo). |
| **Parear dispositivo (QR)…** | Mostra o QR code de pareamento para o celular. |
| **Iniciar com o Windows** | Inicia o DroidDeck quando você entra no Windows. |
| **Sair** | Fecha o DroidDeck. |

### Firewall { #firewall }

O celular se conecta ao PC por **TCP 4787**, e a descoberta automática do servidor usa **UDP 7573**.
Se o Windows perguntar se deve permitir o DroidDeck na rede, permita em **redes privadas**. Se você
fechou esse aviso, crie as regras manualmente em um PowerShell como administrador:

```powershell
New-NetFirewallRule -DisplayName "DroidDeck (TCP 4787)" -Direction Inbound -Protocol TCP -LocalPort 4787 -Profile Private -Action Allow
New-NetFirewallRule -DisplayName "DroidDeck discovery (UDP 7573)" -Direction Inbound -Protocol UDP -LocalPort 7573 -Profile Private -Action Allow
```

Confira se o seu Wi-Fi está definido como rede **Privada** nas configurações do Windows.

## Android

1. No celular, baixe o **`DroidDeck.apk`** da release mais recente.
2. Abra o arquivo e permita a instalação de fontes desconhecidas quando o Android pedir.
3. Abra o **DroidDeck** e siga para o [Pareamento](pairing.md).

É preciso ter Android 7.0 ou mais recente. O app usa a câmera só para ler os QR codes de pareamento.
