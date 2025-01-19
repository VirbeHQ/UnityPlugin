using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Virbe.Core.Data
{
    [Serializable]
    public class ApiBeingConfigv3 : IApiBeingConfig
    {
        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("profile")]
        public Profile Profile { get; set; }

        [JsonProperty("engines")]
        public Engines Engines { get; set; }


        EngineType IApiBeingConfig.ConversationEngine => EngineType.Conversation;

        TTSData IApiBeingConfig.FallbackTTSData => _ttsData;
        private TTSData _ttsData;

        STTData IApiBeingConfig.FallbackSTTData => _sttData;
        private STTData _sttData;

        List<ConversationData> IApiBeingConfig.ConversationData => _conversationData;
        private List<ConversationData> _conversationData = new List<ConversationData>();

        string IApiBeingConfig.LocationId => Profile?.Id;

        AvatarData IApiBeingConfig.AvatarData => _avatarData;
        private AvatarData _avatarData;


        public void Initialize()
        {
            foreach (var convHandler in Engines?.Conversation?.ConnectionHandlers ?? new List<ConnectionHandler>())
            {
                var handler = new ConversationData(GetProtocol(convHandler), convHandler.Path);
                _conversationData.Add(handler);
            }

            if (Engines?.Stt != null)
            {
                var connectionHandler = Engines.Stt.ConnectionHandlers.FirstOrDefault();
                if (connectionHandler != null)
                {
                    _sttData = new STTData(GetProtocol(connectionHandler), connectionHandler.Path);
                }
            }

            if (Engines?.Tts != null)
            {
                var connectionHandler = Engines.Tts.ConnectionHandlers.FirstOrDefault();
                if (connectionHandler != null)
                {
                    _ttsData = new TTSData(GetProtocol(connectionHandler), connectionHandler.Path);
                }
            }
        }

        private ConnectionProtocol GetProtocol(ConnectionHandler handler)
        {
            switch (handler.Protocol)
            {
                case "local":
                    return ConnectionProtocol.local;
                case "http":
                    return ConnectionProtocol.http;
                case "ws":
                    return ConnectionProtocol.ws;
                case "socket-io":
                    return ConnectionProtocol.socket_io;
                case "ws-endless":
                    return ConnectionProtocol.wsEndless;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ConnectionProtocol), handler.Protocol, null);
            }
        }

        void IApiBeingConfig.Localize(LocalizationData data)
        {
            //TODO: implement
        }
    }


    public class Profile
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("touchpoint")]
        public string Touchpoint { get; set; }
    }

    public class AudioEngine
    {
        [JsonProperty("connectionHandlers")]
        public List<ConnectionHandler> ConnectionHandlers { get; set; }
        [JsonProperty("audioParameters")]
        public List<AudioParameters> AudioParameters { get; set; }
    }

    public class Engines
    {
        [JsonProperty("convAi")]
        public Engine Conversation { get; set; }

        [JsonProperty("stt")]
        public AudioEngine Stt { get; set; }

        [JsonProperty("tts")]
        public AudioEngine Tts { get; set; }
    }

    public class Engine
    {
        [JsonProperty("connectionHandlers")]
        public List<ConnectionHandler> ConnectionHandlers { get; set; }
    }

    public class ConnectionHandler
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("protocol")]
        public string Protocol { get; set; }
    }

    public class AudioParameters
    {
        [JsonProperty("channels")]
        public int Channels { get; set; }

        [JsonProperty("frequency")]
        public int Frequency { get; set; }

        [JsonProperty("sampleBits")]
        public int SampleBits { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }
    }
}