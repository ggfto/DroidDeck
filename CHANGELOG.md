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
