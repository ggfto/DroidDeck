using AnyDeck.Auth;
using QRCoder;

namespace AnyDeck.Lib
{
    /// <summary>
    /// Monta os dados de pareamento (URI + QR) compartilhados pela janela de bandeja
    /// e pelo modo de diagnóstico --print-pairing.
    /// </summary>
    public static class PairingInfo
    {
        public const int Port = 5000;

        /// <summary>URI escaneada pelo app: anydeck://pair?ip=..&port=..&key=..</summary>
        public static string BuildUri()
        {
            var ip = NetworkInfo.GetLanIp();
            var key = ApiKeyProvider.GetKey();
            return $"anydeck://pair?ip={ip}&port={Port}&key={key}";
        }

        public static byte[] BuildQrPng(string uri, int pixelsPerModule = 10)
        {
            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
            return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        }
    }
}
