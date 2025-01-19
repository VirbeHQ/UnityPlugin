using System;

namespace Virbe.Core.Data
{
    public struct TTSProcessingArgs
    {
        public readonly string Text;
        public readonly string Lang;
        public readonly string Voice;
        public readonly Guid ID;
        public readonly Action<VoiceData> Callback;

        public TTSProcessingArgs (string text, Guid id, string lang = null, string voice = null, Action<VoiceData> callback = null)
        {
            Text = text;
            Callback = callback;
            ID = id;
            Lang = lang;
            Voice = voice;
        }
    }
}