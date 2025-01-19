using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using Virbe.Core.Actions;
using Virbe.Core.Data;
using Virbe.Core.Logger;

namespace Virbe.Core.Handlers
{
    internal sealed class CommunicationSystem: IDisposable
    {
        public event Action<UserAction> UserActionExecuted;
        //TODO: change card and buttons from BeingAction to ui action
        public event Action<BeingAction> BeingActionExecuted;
        public event Action<VirbeUiAction> UiActionExecuted;
        public event Action<CustomAction> CustomActionExecuted;
        public event Action<VirbeBehaviorAction> BehaviourActionExecuted;
        public event Action<EngineEvent> EngineEventExecuted;
        public event Action<Signal> SignalExecuted;
        public event Action<NamedAction> NamedActionExecuted;

        public event Action<string> UserSpeechRecognized;

        public event Action ConversationDisconnected;
        public event Action ConversationConnected;
        public event Action<string> ConversationInitialized;
        public event Action ConversationReconnecting;

        internal bool Initialized { get; private set; }   

        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(CommunicationSystem));

        private List<ICommunicationHandler> _handlers =new List<ICommunicationHandler>();
        private VirbeUserSession _session;
        private IApiBeingConfig _apiBeingConfig;
        private VirbeBeing _being;
        private ActionToken _callActionToken;

        internal CommunicationSystem(VirbeBeing being, string hostUrl, string profileId, string profileSecret, string appIdentifier)
        {
            if(being == null)
            {
                Debug.LogError($"{nameof(VirbeBeing)} is null, something goes wrong. Please contace virbe support.");
                return;
            }
            var connectionType = ConnectionType.OnDemand;
            _apiBeingConfig = being.ApiBeingConfig;
            _being = being;
            _callActionToken = new ActionToken();
            _callActionToken.UserActionExecuted += (args) => UserActionExecuted?.Invoke(args);
            _callActionToken.BeingActionExecuted += (args) => BeingActionExecuted?.Invoke(args);
            _callActionToken.UserSpeechRecognized += (args) => UserSpeechRecognized?.Invoke(args);

            _callActionToken.UiActionExecuted += (args) => UiActionExecuted?.Invoke(args);
            _callActionToken.CustomActionExecuted += (args) => CustomActionExecuted?.Invoke(args);
            _callActionToken.BehaviourActionExecuted += (args) => BehaviourActionExecuted?.Invoke(args);
            _callActionToken.EngineEventExecuted += (args) => EngineEventExecuted?.Invoke(args);
            _callActionToken.SignalExecuted += (args) => SignalExecuted?.Invoke(args);
            _callActionToken.NamedActionExecuted += (args) => NamedActionExecuted?.Invoke(args);

            _callActionToken.ConversationConnected += () => ConversationConnected?.Invoke();
            _callActionToken.ConversationInitialized += (convId) => ConversationInitialized?.Invoke(convId);
            _callActionToken.ConversationReconnecting += () => ConversationReconnecting?.Invoke();
            _callActionToken.ConversationDisconnected += () => ConversationDisconnected?.Invoke();

            var endpointCoder = new ApiEndpointCoder(appIdentifier, profileId, profileSecret);

            var supportedActions = new List<RequestActionType>();
            var haveRoom = false;
            foreach (var handler in _apiBeingConfig.ConversationData)
            {
                if(handler.ConnectionProtocol == ConnectionProtocol.socket_io)
                {
                    var conversationHandler = new ConversationSocketCommunicationHandler(hostUrl, handler, _callActionToken, connectionType);
                    conversationHandler.SetHeaderUpdate(endpointCoder.UpdateHeaders);
                    conversationHandler.RequestTTSProcessing += (args) => ProcessTTS(args).Forget();
                    _handlers.Add(conversationHandler);
                    if(connectionType == ConnectionType.OnDemand)
                    {
                        _being.UserStartSpeaking += conversationHandler.StartSendingSpeech;
                        _being.UserStopSpeaking += conversationHandler.StopSendingSpeech;
                        conversationHandler.SetAdditionalDisposeAction(() =>
                        {
                            _being.UserStartSpeaking -= conversationHandler.StartSendingSpeech;
                            _being.UserStopSpeaking -= conversationHandler.StopSendingSpeech;
                        });
                    }
                    supportedActions.AddRange(conversationHandler.DefinedActions);
                }
                else if (handler.ConnectionProtocol == ConnectionProtocol.http)
                {
                    //TODO: add rest conversation handler
                    //supportedActions.Add(conversationHandler.DefinedActions);
                }
                //backward compatibility with old API - TODO - remove when fully migrated
                else if (_apiBeingConfig.ConversationEngine == EngineType.Room && handler is RoomData)
                {
                    throw new Exception("Room is no more supported. Migrate to Virbe 2.0");
                }
            }

            if(_apiBeingConfig.ConversationEngine == EngineType.Room && !haveRoom)
            {
                _logger.LogError($"Engine is set to room but no room provided. Could not initialize");
                return;
            }

            if (!supportedActions.Contains(RequestActionType.SendAudio) && !supportedActions.Contains(RequestActionType.SendAudioStream))
            {
                if(_apiBeingConfig.FallbackSTTData.ConnectionProtocol == ConnectionProtocol.socket_io)
                {
                    var socketHandler = new STTSocketCommunicationHandler(hostUrl, _apiBeingConfig.FallbackSTTData, new VirbeEngineLogger(nameof(STTSocketCommunicationHandler)));
                    socketHandler.SetHeaderUpdate(endpointCoder.UpdateHeaders);
                    _being.UserStartSpeaking += socketHandler.OpenSocket;
                    _being.UserStopSpeaking += socketHandler.CloseSocket;
                    socketHandler.SetAdditionalDisposeAction(() =>
                    {
                        _being.UserStartSpeaking -= socketHandler.OpenSocket;
                        _being.UserStopSpeaking -= socketHandler.CloseSocket;
                    });
                    socketHandler.RequestTextSend += (text) => SendText(text).Forget();
                    _handlers.Add(socketHandler);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            if(!supportedActions.Contains(RequestActionType.ProcessTTS))
            {
                if (_apiBeingConfig.FallbackTTSData.ConnectionProtocol == ConnectionProtocol.http)
                {
                    var ttsRestHandler = new TTSCommunicationHandler(hostUrl, _apiBeingConfig.FallbackTTSData, _apiBeingConfig.LocationId, new VirbeEngineLogger(nameof(TTSCommunicationHandler)));
                    ttsRestHandler.SetHeaderUpdate(endpointCoder.UpdateHeaders);
                    _handlers.Add(ttsRestHandler);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        internal async UniTask InitializeWith(Guid endUserId, Guid? conversationId)
        {

            _session = new VirbeUserSession(endUserId, conversationId != null ? conversationId.ToString(): null);
            foreach (var handler in _handlers)
            {
                try
                {
                    await handler.Prepare(_session);
                }
                catch (Exception _)
                {
                    _logger.LogError($"Could not initialize {handler.GetType()}");
                    return;
                }
            }
            Initialized = true;
        }

        internal async UniTask SendText(string text)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.SendText))
                {
                    await handler.MakeAction(RequestActionType.SendText, text);
                }
            }
        }

        internal async UniTask SendSignal(string name, string value)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.SendSignal))
                {
                    await handler.MakeAction(RequestActionType.SendSignal, name, value);
                }
            }
        }

        internal async UniTask SendAudio(byte[] bytes, bool streamed)
        {
            var capability = streamed ? RequestActionType.SendAudioStream : RequestActionType.SendAudio;
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(capability))
                {
                    await handler.MakeAction(capability, bytes);
                }
            }
        }

        internal async UniTaskVoid ProcessTTS(TTSProcessingArgs args)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Initialized && handler.HasCapability(RequestActionType.ProcessTTS))
                {
                    await handler.MakeAction(RequestActionType.ProcessTTS, args.Text, args.Callback);
                    return;
                }
            }
        }

        internal void ClearProcessingQueue()
        {
            foreach(var handler in _handlers)
            {
                handler.ClearProcessingQueue();
            }
        }

        public void Dispose()
        {
            foreach(var handler in _handlers)
            {
                handler.Dispose();
            }
            UserActionExecuted = null;
            BeingActionExecuted = null;
            UserSpeechRecognized = null;
            _handlers.Clear();
            Initialized = false;
        }

        internal class ActionToken
        {
            public Action<UserAction> UserActionExecuted;
            public Action<BeingAction> BeingActionExecuted;
            public Action<string> UserSpeechRecognized;

            public Action<VirbeUiAction> UiActionExecuted;
            public Action<CustomAction> CustomActionExecuted;
            public Action<VirbeBehaviorAction> BehaviourActionExecuted;
            public Action<EngineEvent> EngineEventExecuted;
            public Action<Signal> SignalExecuted;
            public Action<NamedAction> NamedActionExecuted;

            public Action ConversationDisconnected;
            public Action ConversationConnected;
            /// <summary>
            /// Param_1 = conversationID
            /// </summary>
            public Action<string> ConversationInitialized;
            public Action ConversationReconnecting;
        }
    }
}