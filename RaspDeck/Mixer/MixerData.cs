namespace DroidDeck
{
    public class MixerData
    {
        /// <summary>
        /// ID da sess�o de �udio (processo). Se -1, aplica ao dispositivo inteiro.
        /// </summary>
        public int Session { get; set; } = -1;

        /// <summary>
        /// Volume em percentual (0 a 100). Null significa "n�o alterar".
        /// </summary>
        public int? Volume { get; set; }

        /// <summary>
        /// True = mudo, False = n�o mudo, Null = "n�o alterar"
        /// </summary>
        public bool? Mute { get; set; }

        public MixerData() { }

        public MixerData(int? volume = null, bool? mute = null, int session = -1)
        {
            Volume = volume;
            Mute = mute;
            Session = session;
        }
    }
}
