# [1.3.0](https://github.com/ggfto/DroidDeck/compare/v1.2.1...v1.3.0) (2026-07-21)


### Features

* **soundboard:** adiciona soundboard (MyInstants + soundboard nativa do Discord) ([dee42e5](https://github.com/ggfto/DroidDeck/commit/dee42e531ca219930df8e57ccceee59ef3b8e43c))

## [1.2.1](https://github.com/ggfto/DroidDeck/compare/v1.2.0...v1.2.1) (2026-07-07)


### Bug Fixes

* **app:** nao limpa o campo Secret ao salvar credenciais do Discord ([25a14d3](https://github.com/ggfto/DroidDeck/commit/25a14d33b5cd4bdfbac25a2fd824d69fcde9519c))
* **app:** tela de erro 401/403 oferece 'Refazer pareamento' (nao prende o usuario) ([d950ca6](https://github.com/ggfto/DroidDeck/commit/d950ca68d9df1de9450fa121913a94853b75531e))
* **discord:** reconexao automatica continua (watchdog) + serializa ConnectAsync ([f59ed2e](https://github.com/ggfto/DroidDeck/commit/f59ed2e5a776d19a36ac907d515315d260e33ad0))
* **discord:** renova o token OAuth2 via refresh_token (sem reautorizar toda semana) ([73c26fa](https://github.com/ggfto/DroidDeck/commit/73c26fada2d3bd77472b40c2608df4f80fec2757))
* **discord:** timeout do handshake 5s->10s (READY chegava tarde e falhava) ([492745a](https://github.com/ggfto/DroidDeck/commit/492745ac786ed34637cb4aa2e67d5a3f50f60f3a))
* **discord:** trata opcode do frame IPC (responde PONG, trata CLOSE) ([26a7c51](https://github.com/ggfto/DroidDeck/commit/26a7c5113fe99a559741e0017eeab061de10cee3))
* **ram:** libera ico/bitmap GDI, handles de Process e MMDeviceEnumerator no audio ([5512a2c](https://github.com/ggfto/DroidDeck/commit/5512a2cdb1ac082cb33db14da9523fcbdef40912))
* **robustez:** app - reconexao SignalR indefinida, leak de assinatura, mounted, discovery ([62900ab](https://github.com/ggfto/DroidDeck/commit/62900abdbff6294f9f1a5959bd0a6dbfc3cb4ed4))
* **robustez:** backend - discovery resiliente, mixer sem 500, OBS hello, limpeza ([38501ae](https://github.com/ggfto/DroidDeck/commit/38501ae4aad228392510e879581b41b7cb57f6d0))
* **security:** nao vaza a API key em log nem no dialogo de debug do app ([f5ca4ee](https://github.com/ggfto/DroidDeck/commit/f5ca4ee5b7340863aa65354f5fd45484af900802))
* **security:** restringe CORS a loopback/LAN e blinda /api/pairing/local-key contra fetch cross-site ([fce4172](https://github.com/ggfto/DroidDeck/commit/fce41725d6dc3d5051a63bded68850cc8384947a))
* **security:** valida profile.Id contra path traversal ([f98ce35](https://github.com/ggfto/DroidDeck/commit/f98ce354f1b86d95f1a1de04276eae95ad852e91))


### Performance Improvements

* **ram:** cacheia nomes de instancia da GPU e libera counters no shutdown ([d7b612e](https://github.com/ggfto/DroidDeck/commit/d7b612eb09b923539049b1aaa7273314a16093f9))
* **ram:** so faz poll/broadcast com clientes conectados e mede midia a cada 3s ([24f4c67](https://github.com/ggfto/DroidDeck/commit/24f4c67b2cf6c1062d62027165ed91f829bdaeea))

# [1.2.0](https://github.com/ggfto/DroidDeck/compare/v1.1.0...v1.2.0) (2026-06-05)


### Bug Fixes

* **media:** nao cachear o session manager WinRT (RPC_E_WRONG_THREAD) ([4ea7945](https://github.com/ggfto/DroidDeck/commit/4ea79456164e2fe0049576342e64cd7a7c3682be))


### Features

* **media:** controle de midia no deck (play/pause, proxima, anterior, parar) ([2289b33](https://github.com/ggfto/DroidDeck/commit/2289b33fdab57b30050ba0c57324e74ca659d567))
* **media:** play/pause do deck reflete o estado (poller de midia ao vivo) ([3430093](https://github.com/ggfto/DroidDeck/commit/3430093e19550d3c935077220fb15012b216cbef))
* **obs:** controle do OBS via obs-websocket (cenas, gravação, stream) ([88ef320](https://github.com/ggfto/DroidDeck/commit/88ef3202eb963622c096464ff6adb30f5fdb0130))

# [1.1.0](https://github.com/ggfto/DroidDeck/compare/v1.0.0...v1.1.0) (2026-06-03)


### Bug Fixes

* **backend:** em dev servir wwwroot do diretorio atual, nao a copia velha de bin/ ([fa5533f](https://github.com/ggfto/DroidDeck/commit/fa5533f15dcfcdbd6cd239dacef2391c6997b4fd))
* **web:** desliga service worker (sem cache velho no navegador) + titulo DroidDeck ([8a7dc6c](https://github.com/ggfto/DroidDeck/commit/8a7dc6cef5345710e4e8290000b9c16cff9ae0e0))


### Features

* **audio:** junta saidas e entradas numa tela so (split deitado, empilhado em pe) ([76fca67](https://github.com/ggfto/DroidDeck/commit/76fca677e8f2da428a7be60822247713d31f0f35))
* **audio:** modo em pe vira scroll continuo (em vez de split 50/50) ([ddf4f15](https://github.com/ggfto/DroidDeck/commit/ddf4f15cbe0f71b42ff7fca293bed36ce728b63b))
* **discord:** config no web mais visivel (icone no topo) + largura desktop ([de20e73](https://github.com/ggfto/DroidDeck/commit/de20e731ab52c9327d96bb6101dd882f6dbab642))
* **discord:** tela de configuracao no app/web + aviso quando nao configurado ([06b03a5](https://github.com/ggfto/DroidDeck/commit/06b03a565c04e64dd3c2c7735b46965af4ce30a8))
