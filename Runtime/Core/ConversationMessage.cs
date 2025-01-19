using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virbe.Core.Data
{
    public class ConversationMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("conversationId")]
        public string ConversationId { get; set; }

        [JsonProperty("participantId")]
        public string ParticipantId { get; set; }

        [JsonProperty("participantType")]
        public string ParticipantType { get; set; }

        [JsonProperty("action")]
        public MessageAction Action { get; set; }

        [JsonProperty("replyTo")]
        public string ReplyTo { get; set; }

        [JsonProperty("instant")]
        public DateTime? Instant { get; set; }
    }

    public class Conversation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("endUserId")]
        public string EndUserId { get; set; }

        [JsonProperty("profileId")]
        public string ProfileId { get; set; }

        [JsonProperty("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonProperty("modifiedAt")]
        public DateTime? ModifiedAt { get; set; }
    }

    public class MessagesCounts
    {
        [JsonProperty("all")]
        public int All { get; set; }

        [JsonProperty("endUser")]
        public int EndUser { get; set; }

        [JsonProperty("user")]
        public int User { get; set; }

        [JsonProperty("api")]
        public int Api { get; set; }
    }
    public class MessageAction
    {
        [JsonProperty("signal", NullValueHandling = NullValueHandling.Ignore)]
        public Signal Signal { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public TextAction Text { get; set; }

        [JsonProperty("engineEvent", NullValueHandling = NullValueHandling.Ignore)]
        public EngineEvent EngineEvent { get; set; }

        [JsonProperty("endUserStore", NullValueHandling = NullValueHandling.Ignore)]
        public EndUserStore EndUserStore { get; set; }

        [JsonProperty("namedAction", NullValueHandling = NullValueHandling.Ignore)]
        public NamedAction NamedAction { get; set; }

        [JsonProperty("customAction", NullValueHandling = NullValueHandling.Ignore)]
        public CustomAction CustomAction { get; set; }

        [JsonProperty("uiAction", NullValueHandling = NullValueHandling.Ignore)]
        public VirbeUiAction UiAction { get; set; }

        [JsonProperty("behaviorAction", NullValueHandling = NullValueHandling.Ignore)]
        public VirbeBehaviorAction BehaviorAction { get; set; }
    }

    public class Signal
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class TextAction
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("raw", NullValueHandling = NullValueHandling.Ignore)]
        public string Raw { get; set; }

        [JsonProperty("html", NullValueHandling = NullValueHandling.Ignore)]
        public string Html { get; set; }

        [JsonProperty("ssml", NullValueHandling = NullValueHandling.Ignore)]
        public string Ssml { get; set; }

        [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

        [JsonProperty("speechRecognizedState", NullValueHandling = NullValueHandling.Ignore)]
        public string SpeechRecognizedState { get; set; }

        public TextAction(string text, string lang = null)
        {
            Text = text;
            lang = lang ?? string.Empty;
        }
        public TextAction() { }
    }

    public class EngineEvent
    {
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("elapsedTime")]
        public long? ElapsedTime { get; set; }

        [JsonProperty("metaData")]
        public object MetaData { get; set; }
    }

    public class EndUserStore
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class NamedAction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("valueJson")]
        public object ValueJson { get; set; }
    }

    public class CustomAction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    [Serializable]
    public class VirbeUiAction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public VirbeUi Value { get; set; }
    }

    public class VirbeUi
    {
        [JsonProperty("buttons")]
        public List<VirbeButton> Buttons { get; set; }

        [JsonProperty("cards")]
        public List<VirbeCard> Cards { get; set; }

        [JsonProperty("input")]
        public VirbeInput Input { get; set; }

        [JsonProperty("timeoutMs")]
        public int TimeoutMiliseconds { get; set; }
    }

    public class VirbeButton
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("payloadType")]
        public string PayloadType { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; }
    }

    public class VirbeCard
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("payloadType")]
        public string PayloadType { get; set; }

        [JsonProperty("payload")]
        public Payload Payload { get; set; }
    }

    public class Payload
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class VirbeInput
    {
        [JsonProperty("storeKey")]
        public string StoreKey { get; set; }

        [JsonProperty("inputLabel")]
        public string InputLabel { get; set; }

        [JsonProperty("inputType")]
        public string InputType { get; set; }

        [JsonProperty("submitButton")]
        public VirbeButton SubmitButton { get; set; }

        [JsonProperty("cancelButton")]
        public VirbeButton CancelButton { get; set; }
    }

    public class VirbeBehaviorAction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public VirbeBehavior Value { get; set; }
    }

    public class VirbeBehavior
    {
        [JsonProperty("gestures")]
        public List<VirbeAnimation> Gestures { get; set; }

        [JsonProperty("emotions")]
        public List<VirbeAnimation> Emotions { get; set; }
    }

    public class VirbeAnimation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("start")]
        public int Start { get; set; }

        [JsonProperty("duration")]
        public int? Duration { get; set; }
    }

    public class VoiceData
    {
        [JsonProperty("marks")]
        public List<Mark> Marks{ get; set; }
        [JsonProperty("data")]
        public byte[] Data { get; set; }
        public AudioParameters AudioParameters { get; set; }
    }

    public class Mark
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("time")]
        public int Time { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
    }
}