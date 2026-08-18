using DroidDeck.Mixer;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class MixerService
    {
        private readonly ILogger<MixerService> _logger;
        private readonly DroidDeck.Audio.IAudioDeviceEnumerator _enumerator;

        public MixerService(ILogger<MixerService> logger, DroidDeck.Audio.IAudioDeviceEnumerator enumerator)
        {
            _logger = logger;
            _enumerator = enumerator;
        }

        public List<MixerEntity> FindAllOutputs()
        {
            _logger.LogDebug("Finding all output mixers");
            return MixerMaster.GetAllMixers(_enumerator, DataFlow.Render, DeviceState.Active)
                .Select(m => new MixerEntity(m))
                .ToList();
        }

        public List<MixerEntity> FindAllInputs()
        {
            _logger.LogDebug("Finding all input mixers");
            return MixerMaster.GetAllMixers(_enumerator, DataFlow.Capture, DeviceState.Active)
                .Select(m => new MixerEntity(m))
                .ToList();
        }

        public MixerEntity? FindOne(string id)
        {
            _logger.LogDebug("Finding mixer by id {id}", id);
            var master = MixerMaster.GetAllMixers(_enumerator, DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.Id == id)
                      ?? MixerMaster.GetAllMixers(_enumerator, DataFlow.Capture, DeviceState.Active).FirstOrDefault(d => d.Id == id);

            return master != null ? new MixerEntity(master) : null;
        }
    }
}
