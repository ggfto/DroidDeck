import 'package:droiddeck/src/connection/pc_store.dart';
import 'dart:convert';

import 'package:droiddeck/core/core.dart'; // Contains DroidDeckClient, DeckProfile, etc.
import 'package:droiddeck/src/stream_deck/actions/action_catalog.dart';
import 'package:droiddeck/src/stream_deck/layout/grid_placement.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

class StreamDeckController {
  final DroidDeckClient _client = Injector.get<DroidDeckClient>();

  // Signals state
  final isLoading = signal(false);
  final error = signal<String?>(null);
  final profiles = signal<List<DeckProfile>>([]);
  final currentProfile = signal<DeckProfile?>(null);

  /// Mostrando o último deck salvo no aparelho porque o PC não respondeu.
  final offline = signal(false);

  // Cada PC pareado tem o seu deck guardado.
  static String get _cacheKey => PcStore.scoped('deck_cache');
  static String get _lastProfileKey => PcStore.scoped('deck_last_profile');

  Future<void> loadProfiles() async {
    // Primeira carga: mostra na hora o último deck salvo; o do PC substitui quando chegar.
    // Com o PC desligado (sem "connection refused"), a espera pode passar de 20 s.
    if (profiles.value.isEmpty) {
      final cached = await _loadCache();
      if (cached.isNotEmpty && profiles.value.isEmpty) {
        profiles.value = cached;
        _selectInitial(cached);
      }
    }
    try {
      isLoading.value = profiles.value.isEmpty;
      error.value = null;
      final result = await _client.getProfiles();
      profiles.value = result;
      offline.value = false;
      // Se o app abriu sem o PC, o catálogo de ações (e os estados ao vivo que ele
      // assina, como os timers) ainda não carregou: tenta agora. Já carregado = nada.
      ActionCatalog.instance.load();
      _selectInitial(result);
      _saveCache(result);
    } catch (e) {
      error.value = e.toString();
      // Sem PC: fica o último deck que o aparelho viu (carregado acima), avisando.
      offline.value = profiles.value.isNotEmpty;
    } finally {
      isLoading.value = false;
    }
  }

  /// Abre no perfil em que o usuário estava da última vez (ou mantém o atual).
  Future<void> _selectInitial(List<DeckProfile> list) async {
    if (list.isEmpty) return;
    final current = currentProfile.value;
    if (current != null) {
      // Recarga: pega a versão nova do mesmo perfil.
      currentProfile.value =
          list.firstWhere((p) => p.id == current.id, orElse: () => list.first);
      return;
    }
    String? last;
    try {
      last = (await SharedPreferences.getInstance()).getString(_lastProfileKey);
    } catch (_) {}
    currentProfile.value =
        list.firstWhere((p) => p.id == last, orElse: () => list.first);
  }

  /// Lembra o perfil visível para abrir nele da próxima vez.
  Future<void> rememberProfile(String id) async {
    try {
      await (await SharedPreferences.getInstance()).setString(_lastProfileKey, id);
    } catch (_) {}
  }

  Future<void> _saveCache(List<DeckProfile> list) async {
    if (WebMode.configurator) return; // o configurador roda no próprio PC
    try {
      final json = jsonEncode(list.map((p) => p.toJson()).toList());
      await (await SharedPreferences.getInstance()).setString(_cacheKey, json);
    } catch (_) {}
  }

  Future<List<DeckProfile>> _loadCache() async {
    if (WebMode.configurator) return const [];
    try {
      final raw = (await SharedPreferences.getInstance()).getString(_cacheKey);
      if (raw == null) return const [];
      return (jsonDecode(raw) as List)
          .map((e) => DeckProfile.fromJson(Map<String, dynamic>.from(e as Map)))
          .toList();
    } catch (_) {
      return const [];
    }
  }

  /// Executa a ação de um gesto ('tap', 'longPress', 'doubleTap'). Devolve false se o
  /// PC não respondeu, para a tela avisar.
  Future<bool> executeButtonAction(DeckButton button,
      {String gesture = 'tap'}) async {
    // Botão de dois estados vai ao PC mesmo sem ação: o toque alterna o estado lá.
    final toggles = gesture == 'tap' && button.toggle;
    if (button.actionFor(gesture) == null && !toggles) return true;

    // O PC executa a ação salva no perfil, pelo id do botão: o celular não escolhe o
    // que roda. Procura o perfil dono do botão (a página atual, ou uma pasta aberta).
    final current = currentProfile.value;
    final owner = current != null && current.buttons.any((b) => b.id == button.id)
        ? current
        : profiles.value
            .where((p) => p.buttons.any((b) => b.id == button.id))
            .firstOrNull;
    if (owner == null) return true;

    try {
      await _client.pressButton(owner.id, button.id, gesture: gesture);
      return true;
    } catch (e) {
      debugPrint('Error executing action: $e');
      if (e is DioException && e.response != null) {
        // O PC respondeu, mas o botão não existe mais lá: o deck daqui está velho.
        if (e.response!.statusCode == 404) loadProfiles();
        return true;
      }
      return false;
    }
  }

  /// Mensagem que o PC mandou ao recusar o pedido (403), se houver.
  static String? _forbiddenMessage(Object e) {
    if (e is! DioException || e.response?.statusCode != 403) return null;
    final data = e.response?.data;
    if (data is Map && data['error'] is String) return data['error'] as String;
    return tr('O PC recusou a alteração.');
  }

  /// Salva o botão. Devolve a mensagem de recusa do PC (ex.: tentar criar no celular um
  /// botão que abre programas) para a tela mostrar; outros erros vão para [error].
  Future<String?> updateButton(DeckButton newButton) async {
    final profile = currentProfile.value;
    if (profile == null) return null;

    try {
      isLoading.value = true;

      // Update local state first (optimistic)
      final updatedButtons = List<DeckButton>.from(profile.buttons);
      final index = updatedButtons.indexWhere(
          (b) => b.row == newButton.row && b.column == newButton.column);

      if (index != -1) {
        // If button is "empty/cleared" (id empty), we could remove it or just update it
        // If it's a clearing operation, best to keep the object but reset props, OR remove from list if sparse.
        // Given we use sparse list in frontend now:
        if (newButton.id.isEmpty && index != -1) {
          // It's a clear operation
          updatedButtons.removeAt(index);
        } else {
          updatedButtons[index] = newButton;
        }
      } else if (newButton.id.isNotEmpty) {
        // New button
        updatedButtons.add(newButton);
      }

      final updatedProfile = profile.copyWith(buttons: updatedButtons);

      // Save to backend
      await _client.saveProfile(updatedProfile);

      // Update signal
      currentProfile.value = updatedProfile;

      // Also update list if needed
      final profileIndex = profiles.value.indexWhere((p) => p.id == profile.id);
      if (profileIndex != -1) {
        profiles.value[profileIndex] = updatedProfile;
        profiles.value = List.from(profiles.value); // Trigger notify
      }
    } catch (e) {
      final forbidden = _forbiddenMessage(e);
      if (forbidden != null) return forbidden;
      error.value = e.toString();
    } finally {
      isLoading.value = false;
    }
    return null;
  }

  Future<void> updateProfileFull(DeckProfile updatedProfile) async {
    try {
      isLoading.value = true;
      // Optimistic update
      currentProfile.value = updatedProfile;

      // Update in list
      final index = profiles.value.indexWhere((p) => p.id == updatedProfile.id);
      if (index != -1) {
        profiles.value[index] = updatedProfile;
        profiles.value = List.from(profiles.value); // Trigger notify
      }

      await _client.saveProfile(updatedProfile);
    } catch (e) {
      error.value = e.toString();
    } finally {
      isLoading.value = false;
    }
  }

  void switchProfile(String profileId) {
    try {
      final profile = profiles.value.firstWhere((p) => p.id == profileId);
      currentProfile.value = profile;
    } catch (e) {
      debugPrint('Profile not found: $profileId');
    }
  }

  Future<void> deleteProfile(String id) async {
    try {
      isLoading.value = true;
      await _client.deleteProfile(id);

      // Remove from list
      profiles.value.removeWhere((p) => p.id == id);
      profiles.value = List.from(profiles.value); // Notify

      // If current profile was deleted, switch
      if (currentProfile.value?.id == id) {
        if (profiles.value.isNotEmpty) {
          currentProfile.value = profiles.value.first;
        } else {
          currentProfile.value = null;
        }
      }
    } catch (e) {
      error.value = e.toString();
    } finally {
      isLoading.value = false;
    }
  }

  /// Move um botão para (newRow,newCol); troca de lugar se o destino estiver ocupado.
  Future<void> moveButton(DeckButton button, int newRow, int newCol) async {
    final profile = currentProfile.value;
    if (profile == null) return;
    if (button.row == newRow && button.column == newCol) return;

    final buttons = List<DeckButton>.from(profile.buttons);
    final srcIdx = buttons
        .indexWhere((b) => b.row == button.row && b.column == button.column);
    if (srcIdx == -1) return;
    final targetIdx =
        buttons.indexWhere((b) => b.row == newRow && b.column == newCol);

    if (targetIdx != -1 && targetIdx != srcIdx) {
      // Troca: o botão que estava no destino vai para a posição de origem.
      buttons[targetIdx] =
          _copyWithPos(buttons[targetIdx], button.row, button.column);
    }
    buttons[srcIdx] = _copyWithPos(buttons[srcIdx], newRow, newCol);

    await updateProfileFull(profile.copyWith(buttons: buttons));
  }

  /// Copia (ou move) [button] para a primeira casa livre de [target], numa grade de
  /// [rows]×[columns] (a do celular: botão fora dela some). Devolve o erro para mostrar,
  /// ou null se deu certo.
  Future<String?> copyButtonTo(DeckButton button, DeckProfile target,
      {required int rows, required int columns, required bool move}) async {
    final owner = profiles.value
        .where((p) => p.buttons.any((b) => b.id == button.id))
        .firstOrNull;
    if (owner == null) return tr('O botão não está mais no PC. Atualize o deck.');

    final layout = GridLayout.of(target.buttons, rows, columns);
    // Casa com botão escondido (fora da grade de outro aparelho) também conta como ocupada.
    final anchors = {for (final b in target.buttons) GridLayout.key(b.row, b.column)};
    (int, int)? free;
    for (var r = 0; r < rows && free == null; r++) {
      for (var c = 0; c < columns; c++) {
        if (layout.isFree(r, c) && !anchors.contains(GridLayout.key(r, c))) {
          free = (r, c);
          break;
        }
      }
    }
    if (free == null) {
      return tr('"{name}" está cheio. Libere uma casa e tente de novo.', {'name': target.name});
    }

    try {
      await _client.copyButton(
        fromProfileId: owner.id,
        buttonId: button.id,
        toProfileId: target.id,
        row: free.$1,
        column: free.$2,
        move: move,
      );
      await loadProfiles();
      return null;
    } catch (e) {
      debugPrint('Error copying button: $e');
      return tr('O PC não respondeu. Confira se o DroidDeck está aberto nele.');
    }
  }

  /// Ajusta as dimensões (linhas/colunas) do perfil atual.
  Future<void> setGridSize(int rows, int columns) async {
    final profile = currentProfile.value;
    if (profile == null) return;
    await updateProfileFull(profile.copyWith(
      rows: rows.clamp(1, 10),
      columns: columns.clamp(1, 10),
    ));
  }

  DeckButton _copyWithPos(DeckButton b, int row, int col) =>
      b.copyWith(row: row, column: col);
}
