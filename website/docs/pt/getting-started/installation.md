# Instalação

## Você precisa das duas partes

<div class="dd-parts" markdown>

<div class="dd-part" markdown>
<p class="dd-part__text"><strong>1. DroidDeck para Windows</strong> executa as ações no seu PC. Ele fica na bandeja do sistema.</p>

[:material-microsoft-windows: App para Windows](https://github.com/ggfto/DroidDeck/releases/latest/download/DroidDeck-Setup.exe){ .md-button .md-button--primary }
</div>

<div class="dd-part" markdown>
<p class="dd-part__text"><strong>2. DroidDeck para Android</strong> é o deck: os botões que você toca. Baixe no celular.</p>

[:material-android: App para Android](https://github.com/ggfto/DroidDeck/releases/latest/download/DroidDeck.apk){ .md-button .md-button--primary }
</div>

</div>

Os dois arquivos também vêm anexados em toda [release no GitHub](https://github.com/ggfto/DroidDeck/releases/latest).
Não tem app para iPhone? O deck também roda em [qualquer navegador](browser-usb.md#browser).

## Windows

1. Baixe o **`DroidDeck-Setup.exe`**.

    [:material-download: Baixar o DroidDeck-Setup.exe](https://github.com/ggfto/DroidDeck/releases/latest/download/DroidDeck-Setup.exe){ .md-button .md-button--primary }

2. Execute. A instalação é por usuário (não pede administrador) e cria atalhos na área de trabalho
   e no menu Iniciar.

    !!! note "O Windows protegeu o computador?"
        Pode executar sem medo: o instalador ainda não tem assinatura de código, então o Windows
        SmartScreen pode mostrar *"O Windows protegeu o computador"*. Clique em
        **Mais informações → Executar assim mesmo**.

3. Na primeira execução, o DroidDeck liga o **Iniciar com o Windows** e oferece liberar o
   [firewall](#firewall) para a rede local. Aceite, para o celular conseguir chegar ao PC.

O app não tem janela. Ele fica na **bandeja do sistema** (pode estar atrás da seta **^**), e uma
notificação confirma que está rodando em `http://localhost:4787/`. O build é autocontido, então você
não precisa instalar o .NET.

O DroidDeck instalado **se atualiza sozinho**: ele procura versões novas nas releases do GitHub,
baixa em segundo plano e aplica a atualização na próxima vez que abrir. Para atualizar na hora, use
**Procurar atualização** no menu da bandeja. Para desinstalar, use *Configurações → Aplicativos* do
Windows.

??? note "Prefere sem instalar?"
    A release também traz o **`DroidDeck-win-x64.zip`**. Extraia em uma pasta definitiva e execute o
    `DroidDeck.exe`. Essa cópia não se atualiza sozinha: o item **Baixar versão mais recente…** da
    bandeja abre a página da release.

## Android

1. No celular, baixe o **`DroidDeck.apk`**.

    [:material-download: Baixar o DroidDeck.apk](https://github.com/ggfto/DroidDeck/releases/latest/download/DroidDeck.apk){ .md-button .md-button--primary }

2. Abra o arquivo e permita a instalação de fontes desconhecidas quando o Android pedir.
3. Abra o **DroidDeck** e siga para o [Pareamento](pairing.md).

É preciso ter Android 7.0 ou mais recente. O app usa a câmera só para ler os QR codes de pareamento.

!!! tip "Atualizações no Android"
    O APK não se atualiza sozinho. O [Obtainium](https://github.com/ImranR98/Obtainium) acompanha as
    releases do GitHub e oferece cada versão nova: [adicione o DroidDeck ao Obtainium](https://apps.obtainium.imranr.dev/redirect?r=obtainium://add/https://github.com/ggfto/DroidDeck).

**Próximo passo: [pareie o celular com o PC](pairing.md).**

## Menu da bandeja { #tray-menu }

Clique com o botão direito no ícone do DroidDeck na bandeja (ele pode estar atrás da seta **^**):

<figure class="dd-figure" markdown>
![Menu da bandeja do DroidDeck: Abrir no navegador, Parear dispositivo (QR), Iniciar com o Windows, Liberar no firewall (rede local), Conexão USB (cabo), a versão, Procurar atualização e Sair](../../assets/screenshots/tray-menu-pt.png){ .dd-shot width="440" height="412" loading="lazy" }
<figcaption>O menu da bandeja. As marcas mostram as opções ligadas.</figcaption>
</figure>

??? note "O que cada item da bandeja faz"

    | Item | O que faz |
    |---|---|
    | **Abrir no navegador** | Abre o configurador em `http://localhost:4787/` (o clique duplo no ícone faz o mesmo). |
    | **Parear dispositivo (QR)…** | Mostra o QR code de pareamento para o celular. |
    | **Iniciar com o Windows** | Inicia o DroidDeck quando você entra no Windows. |
    | **Liberar no firewall (rede local)** | Cria as regras do [firewall](#firewall). Marcado quando elas já existem. |
    | **Conexão USB (cabo)** | Conecta um celular Android pelo [cabo USB](browser-usb.md#usb). |
    | **Procurar atualização** | Procura uma versão nova e, quando há uma baixada, vira **Reiniciar para atualizar**. |
    | **Sair** | Fecha o DroidDeck. |

## Firewall { #firewall }

O celular se conecta ao PC por **TCP 4787**, e a descoberta automática do servidor usa **UDP 7573**.
O instalador oferece liberar as duas na primeira execução. Se você pulou essa etapa, use o item
**Liberar no firewall (rede local)** da bandeja. O Windows pede permissão de administrador, e as
regras valem só para aparelhos da sua sub-rede local, mesmo que o Wi-Fi esteja marcado como rede
Pública.

??? note "Criar as regras à mão"
    Num PowerShell como administrador:

    ```powershell
    New-NetFirewallRule -DisplayName "DroidDeck (TCP 4787)" -Direction Inbound -Protocol TCP -LocalPort 4787 -RemoteAddress LocalSubnet -Action Allow
    New-NetFirewallRule -DisplayName "DroidDeck discovery (UDP 7573)" -Direction Inbound -Protocol UDP -LocalPort 7573 -RemoteAddress LocalSubnet -Action Allow
    ```
