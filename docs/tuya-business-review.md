# Pedido de registro próprio de app à Tuya (business review)

Rascunho pronto para enviar. Objetivo: obter um `client_id` + `schema` próprios do DroidDeck
para o login por QR, substituindo o registro público do Home Assistant que usamos hoje.

## Por que isso importa

Hoje o pareamento usa `client_id=HA_3y9q4ak7g4ephrvke` / `schema=haauthorize`, o registro
público do Home Assistant. Consequências:

1. **O usuário vê "Home Assistant"** na tela de autorização do Smart Life ao parear o
   DroidDeck. É confuso e parece erro.
2. **Dependemos de terceiro.** Se a Tuya ou o projeto Home Assistant revogarem ou rotacionarem
   esse registro, o pareamento do DroidDeck para de funcionar sem que a gente tenha feito nada.
3. **Apps de marca recusam.** Nova Digital, Positivo, RSmart e afins respondem
   _"please use the designated app to scan the code to login"_ — só Smart Life e Tuya Smart
   aceitam esse schema. Hoje a saída é o usuário reparear o aparelho no Smart Life.

**Não é bloqueante.** A integração funciona; isto é higiene e redução de risco.

## O que muda quando aprovarem

Só configuração. `clientId` e `schema` moram em `%LocalAppData%\DroidDeck\tuya.json`
(campos de `TuyaConfig`, em `RaspDeck/Services/Tuya/TuyaModels.cs`), com o registro do HA
apenas como valor padrão. Trocar não exige recompilar nem publicar release — basta editar
o arquivo, ou mudar o default e soltar uma versão nova.

Vale confirmar com eles se o registro novo cobre os apps OEM. Se cobrir, some a necessidade
de repareamento e o README pode perder aquela seção de ressalva.

## Onde enviar

O caminho conhecido é **mensagem privada no fórum de desenvolvedores da Tuya**
(https://tuyaos.com) — foi o que um moderador indicou na thread sobre vincular dispositivos
SmartLife via OAuth. Vale abrir **também um ticket de suporte** em https://iot.tuya.com
(Service → Ticket), porque o fórum costuma ser lento.

> ⚠️ Confirmar o canal atual antes de mandar: a Tuya reorganiza esses fluxos com frequência,
> e a informação acima é de agosto/2026.

Eles pedem: se você é desenvolvedor **empresa** (com nome da empresa) ou **individual**, e
qual o **cenário de uso**. O texto abaixo já responde as duas coisas.

---

## Texto para enviar (inglês)

> **Subject:** Request for app authorization credentials (client_id / schema) for an
> open-source Stream Deck project
>
> Hello,
>
> I am an **individual developer** working on **DroidDeck**, a free and open-source project
> that turns an Android phone into a Stream Deck-style control surface for a Windows PC
> (https://github.com/ggfto/DroidDeck). It is not a commercial product and is not sold.
>
> I recently added smart home support so users can toggle lights, plugs and switches from the
> deck. The integration uses your official `tuya-device-sharing-sdk` with the QR-code login
> flow, which works well and requires no Tuya IoT cloud project from the end user.
>
> **My request:** I would like to obtain a `client_id` and `schema` of my own for this QR
> authorization flow.
>
> Today the integration falls back to the publicly known Home Assistant registration
> (`schema=haauthorize`), because I could not find a self-service way to register an app for
> this flow. That is not a good situation for anyone:
>
> - End users are asked to authorize **"Home Assistant"** when pairing a completely different
>   application, which is confusing and looks like a security problem.
> - My project depends on a registration I do not own and cannot maintain.
>
> I would much rather use credentials that correctly identify my application.
>
> **Usage scenario:** the user opens DroidDeck's settings, enters the User Code from their
> Smart Life app, and scans a QR code to link their account. DroidDeck then lists the user's
> devices, sends commands when a deck button is pressed, and subscribes to the MQTT push
> channel to keep the button state in sync. No polling — device state comes exclusively from
> push messages. Expected scale is small: individual hobbyist users, each with their own
> account and a handful of devices.
>
> **One additional question:** would such a registration be accepted by OEM-branded apps built
> on Tuya (for example Nova Digital, widely used in Brazil), or only by Smart Life and Tuya
> Smart? Currently users of branded apps get *"please use the designated app to scan the code
> to login"* and have to re-pair their device in Smart Life, which is a rough experience.
>
> Happy to provide any further information you need.
>
> Thank you,
> Gabriel Freitas

---

## Preencher antes de enviar

- [ ] Confirmar o canal (fórum vs. ticket) — ver aviso acima
- [ ] Conferir se quer se apresentar como individual ou como empresa (muda o tratamento deles)
- [ ] Decidir se cita o número de usuários; hoje é projeto pessoal, e inflar isso não ajuda
- [ ] Guardar o número do ticket/thread aqui embaixo para acompanhar

## Acompanhamento

| Data | Canal | Status |
|------|-------|--------|
|      |       | (ainda não enviado) |
