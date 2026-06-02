import 'package:companion/core/core.dart'; // Contains AnyDeckClient, DeckProfile, etc.
import 'package:flutter/foundation.dart';

class StreamDeckController {
  final AnyDeckClient _client = Injector.get<AnyDeckClient>();

  // Signals state
  final isLoading = signal(false);
  final error = signal<String?>(null);
  final profiles = signal<List<DeckProfile>>([]);
  final currentProfile = signal<DeckProfile?>(null);

  Future<void> loadProfiles() async {
    try {
      isLoading.value = true;
      error.value = null;
      final result = await _client.getProfiles();
      profiles.value = result;

      if (currentProfile.value == null && result.isNotEmpty) {
        currentProfile.value = result.first;
      }
    } catch (e) {
      error.value = e.toString();
    } finally {
      isLoading.value = false;
    }
  }

  Future<void> executeButtonAction(DeckButton button) async {
    if (button.action == null) return;

    try {
      // Execute the single action
      await _client.executeAction(button.action!);
    } catch (e) {
      debugPrint('Error executing action: $e');
    }
  }

  Future<void> updateButton(DeckButton newButton) async {
    final profile = currentProfile.value;
    if (profile == null) return;

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

      final updatedProfile = DeckProfile(
        id: profile.id,
        name: profile.name,
        rows: profile.rows,
        columns: profile.columns,
        buttons: updatedButtons,
      );

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
      error.value = e.toString();
    } finally {
      isLoading.value = false;
    }
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

    await updateProfileFull(DeckProfile(
      id: profile.id,
      name: profile.name,
      rows: profile.rows,
      columns: profile.columns,
      isDefault: profile.isDefault,
      buttons: buttons,
    ));
  }

  /// Ajusta as dimensões (linhas/colunas) do perfil atual.
  Future<void> setGridSize(int rows, int columns) async {
    final profile = currentProfile.value;
    if (profile == null) return;
    await updateProfileFull(DeckProfile(
      id: profile.id,
      name: profile.name,
      rows: rows.clamp(1, 10),
      columns: columns.clamp(1, 10),
      isDefault: profile.isDefault,
      buttons: profile.buttons,
    ));
  }

  DeckButton _copyWithPos(DeckButton b, int row, int col) => DeckButton(
        id: b.id,
        row: row,
        column: col,
        label: b.label,
        iconBase64: b.iconBase64,
        iconName: b.iconName,
        backgroundColor: b.backgroundColor,
        activeColor: b.activeColor,
        action: b.action,
        dynamicType: b.dynamicType,
      );
}
