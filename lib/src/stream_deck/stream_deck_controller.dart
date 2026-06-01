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
      print('Profile not found: $profileId');
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
}
