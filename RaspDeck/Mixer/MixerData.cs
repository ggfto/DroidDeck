namespace AnyDeck
{
    public class MixerData
    {
        /// <summary>
        /// ID da sessão de áudio (processo). Se -1, aplica ao dispositivo inteiro.
        /// </summary>
        public int Session { get; set; } = -1;

        /// <summary>
        /// Volume em percentual (0 a 100). Null significa "não alterar".
        /// </summary>
        public int? Volume { get; set; }

        /// <summary>
        /// True = mudo, False = não mudo, Null = "não alterar"
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
