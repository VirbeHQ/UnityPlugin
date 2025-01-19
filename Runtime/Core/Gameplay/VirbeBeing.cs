using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Virbe.Core.Actions;
using Virbe.Core.Data;
using Virbe.Core.Handlers;
using Virbe.Core.Logger;
using Virbe.Core.ThirdParty.SavWav;
using Virbe.Core.VAD;

namespace Virbe.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirbeActionPlayer))]
    public class VirbeBeing : MonoBehaviour
    {
        public event Action<BeingState> OnBeingStateChanged;
        public event Action<UserAction> OnUserAction;
        public event Action<BeingAction> OnBeingAction;
        public event Action<bool> OnBeingMuteChange;
        public event Action<string> UserSpeechRecognized;

        public event Action<VirbeUiAction> OnUiAction;
        public event Action<CustomAction> OnCustomAction;
        public event Action<VirbeBehaviorAction> OnBehaviourAction;
        public event Action<EngineEvent> OnEngineEvent;
        public event Action<Signal> OnSignal;
        public event Action<NamedAction> OnNamedAction;

        public Behaviour CurrentBeingBehaviour => _currentState._behaviour;
        public bool IsBeingSpeaking => _virbeActionPlayer.HasActionsToPlay;
        public IApiBeingConfig ApiBeingConfig { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? ConversationId { get; private set; }

        internal event Action ConversationStarted;
        internal event Action ConversationEnded;

        internal event Action UserStartSpeaking;
        internal event Action UserStopSpeaking;
        internal event Action UserLeftConversation;

        private string _baseUrl;
        [SerializeField] private string _ApiUrl;
        [SerializeField] private string _ProfileID;
        [SerializeField] private string _ProfileSecret;
        [SerializeField] private bool _AutoInitialize = true;
        [SerializeField] protected internal bool _AutoStartConversation = false;

        [SerializeField] private bool _PersistentUserID;
        [SerializeField] private bool _PersistentConversationID;

        [SerializeField] private float focusedStateTimeout = 20f;
        [SerializeField] private float inConversationStateTimeout = 10f;
        [SerializeField] private float listeningStateTimeout = 8f;
        [SerializeField] private float requestErrorStateTimeout = .1f;

        [Header("Being Events")]
        [SerializeField] private UnityEvent<BeingState> onBeingStateChange = new BeingStateChangeEvent();
        [SerializeField] private UnityEvent<bool> onBeingMuteChange = new UnityEvent<bool>();
        [SerializeField] private UnityEvent<string> userSpeechRecognized;

        [SerializeField] private UserActionEvent onUserAction = new UserActionEvent();
        [SerializeField] private BeingActionEvent onBeingAction = new BeingActionEvent();
        [SerializeField] private ConversationErrorEvent onConversationError = new ConversationErrorEvent();

        [SerializeField] private UnityEvent<VirbeUiAction> onUiAction;
        [SerializeField] private UnityEvent<CustomAction> onCustomAction;
        [SerializeField] private UnityEvent<VirbeBehaviorAction> onBehaviourAction;
        [SerializeField] private UnityEvent<EngineEvent> onEngineEvent;
        [SerializeField] private UnityEvent<Signal> onSignal;
        [SerializeField] private UnityEvent<NamedAction> onNamedAction;

        private readonly BeingState _currentState = new BeingState();
        private readonly VirbeEngineLogger _logger = new VirbeEngineLogger(nameof(VirbeBeing));

        private string _overriddenSttLangCode = null;
        private string _overriddenTtsLanguage = null;

        private CommunicationSystem _communicationSystem;

        private string _beingConfigJson;
        private VirbeActionPlayer _virbeActionPlayer;
        private Coroutine _autoBeingStateChangeCoroutine;
        private bool _saveWaveSamplesDebug = false;
        private bool _initialized;
        private string _appIdentifer;
        private const string _userIdKey = "endUserIdKey";
        private const string _conversationIdKey = "conversationIdKey";

        private void Awake()
        {
            _appIdentifer = Application.identifier;
            _virbeActionPlayer = GetComponent<VirbeActionPlayer>();
            _initialized = false;
            UserId = Guid.NewGuid();
            ConversationId = null;

            if (_PersistentUserID)
            {
                if (PlayerPrefs.HasKey(_userIdKey))
                {
                    var id = PlayerPrefs.GetString(_userIdKey);
                    if (Guid.TryParse(id, out var parsedGuid))
                    {
                        UserId = parsedGuid;
                    }
                }
                else
                {
                    PlayerPrefs.SetString(_userIdKey, UserId.ToString());
                }
            }
            else
            {
                PlayerPrefs.DeleteKey(_userIdKey);
            }

            if (_PersistentConversationID)
            {
                if (PlayerPrefs.HasKey(_conversationIdKey))
                {
                    var id = PlayerPrefs.GetString(_conversationIdKey);
                    if(Guid.TryParse(id, out var parsedGuid))
                    {
                        ConversationId = parsedGuid;
                    }
                }
            }
            else
            {
                PlayerPrefs.DeleteKey(_conversationIdKey);
            }

            if (_AutoInitialize)
            {
                InitializeBeing(_ApiUrl, _ProfileID, _ProfileSecret);
            }
        }

        private void OnDisable()
        {
            if (_PersistentConversationID && ConversationId != null && ConversationId != Guid.Empty)
            {
                try
                {
                    PlayerPrefs.SetString(_conversationIdKey, ConversationId.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        }

        private void Start()
        {
            ChangeBeingState(Behaviour.Idle);
        }

        private void OnDestroy()
        {
            _communicationSystem?.Dispose();
            StopAllCoroutines();
        }

        public void SetSettings(bool autoStartConversation = true, bool persistentUserId = false, float focusedStateTimeout = 60, float inConversationTimeout = 30, float listeningTimeout = 10)
        {
            this._PersistentUserID = persistentUserId;
            this._AutoStartConversation = autoStartConversation;
            this.focusedStateTimeout = focusedStateTimeout;
            this.inConversationStateTimeout = inConversationTimeout;
            this.listeningStateTimeout = listeningTimeout;
        }

        public void InitializeBeing() => InitializeBeing(_ApiUrl, _ProfileID, _ProfileSecret);

        public void InitializeBeing(string apiUrl, string profileID, string profileSecret)
        {
            var properUrl = VirbeUtils.TryCreateUrlAddress(apiUrl, out var uri);
            if (!properUrl)
            {
                _logger.Log("There is no Base URL or it is wrong, coul not initialize");
                return;
            }
            _baseUrl = uri.GetLeftPart(UriPartial.Authority);
            StartCoroutine(DownloadConfig(uri, profileID, profileSecret).ToCoroutine());
        }

        public void StartNewConversationWithUserSession(Guid endUserId, Guid conversationId) => RestoreConversation(endUserId, conversationId).Forget();

        public async UniTask StartNewConversation(Guid? endUserId = null)
        {
            if(!_initialized)
            {
                _logger.LogError($"No api being config provided, can't start new coonversation");
                return;
            }

            StopCurrentAndScheduledActions();
            await _communicationSystem.InitializeWith(endUserId ?? Guid.NewGuid(), ConversationId);
            ConversationStarted?.Invoke();
        }

        public void StopConversation()
        {
            StopCurrentAndScheduledActions();
            ConversationEnded?.Invoke();
        }

        public void UserHasApproached(bool forceNewConversation = false)
        {
            if (!_initialized)
            {
                _logger.Log("Being not initialized");
                return;
            }
            if (CanChangeBeingState(Behaviour.Focused))
            {
                if (forceNewConversation && _currentState._behaviour == Behaviour.Idle)
                {
                    ConversationId = null;
                    StartNewConversation().Forget();
                }

                ChangeBeingState(Behaviour.Focused);
            }
        }

        public void UserHasEngagedInConversation()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
        }

        public void UserHasStartedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.Listening))
            {
                ChangeBeingState(Behaviour.Listening);
            }
            UserStartSpeaking?.Invoke();
        }

        public void UserHasStoppedSpeaking()
        {
            if (CanChangeBeingState(Behaviour.InConversation))
            {
                ChangeBeingState(Behaviour.InConversation);
            }
            UserStopSpeaking?.Invoke();
        }

        public void UserHasDisengagedFromConversation()
        {
            if (CanChangeBeingState(Behaviour.Focused))
            {
                ChangeBeingState(Behaviour.Focused);
            }
        }

        public void UserHasLeft()
        {
            if (CanChangeBeingState(Behaviour.Idle))
            {
                ChangeBeingState(Behaviour.Idle);
            }
            UserLeftConversation?.Invoke();
        }

        public void SetBeingMute(bool isMuted)
        {
            _currentState.isMuted = isMuted;
            if (_virbeActionPlayer)
            {
                _virbeActionPlayer.MuteAudio(isMuted);
            }
            onBeingMuteChange.Invoke(_currentState.isMuted);
            OnBeingMuteChange?.Invoke(_currentState.isMuted);
        }

        public void SetOverrideDefaultSttLangCode(string sttLangCode)
        {
            _overriddenSttLangCode = sttLangCode;
        }

        public void SetOverrideDefaultTtsLanguage(string ttsLanguage)
        {
            _overriddenTtsLanguage = ttsLanguage;
        }

        public void SendSpeechBytes(float[] recordedAudio, bool streamed)
        {
            if (!_initialized)
            {
                _logger.Log("Being not initialized");
                return;
            }
            if (recordedAudio == null)
            {
                _logger.Log("Cannot send empty speech bytes");
                return;
            }

            var recordingBytes = SavWav.GetWavF(
                  samples: recordedAudio,
                  frequency: (uint)Mic.Instance.Frequency,
                  channels: (ushort)Mic.Instance.Channels,
                  length: out _
              );

            if (_saveWaveSamplesDebug)
            {
                var path = Path.Combine(Application.dataPath, "TestRecordings");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                File.WriteAllBytes($"{Application.dataPath}/TestRecordings/{DateTime.Now:HH_mm_ss}.wav",
                    recordingBytes);
            }
            _communicationSystem.SendAudio(recordingBytes, streamed).Forget();
        }

        public void SubmitInput(string submitPayload, string storeKey, string storeValue)
        {
            // TODO send inputs to room api
        }

        public void SendText(string capturedUtterance)
        {
            if (!_initialized)
            {
                _logger.Log("Being not initialized");
                return;
            }
            _communicationSystem.SendText(capturedUtterance).Forget();
        }

        public void StopCurrentAndScheduledActions()
        {
            _virbeActionPlayer.StopCurrentAndScheduledActions();
            if (!_initialized)
            {
                _logger.Log("Being not initialized");
                return;
            }
            _communicationSystem.ClearProcessingQueue();
        }

        private async UniTask DownloadConfig(Uri settingsUri, string profileID, string profileSecret)
        {
            var downloader = new BeingConfigDownloader(settingsUri, profileID, profileSecret, _appIdentifer);
            _beingConfigJson = await downloader.DownloadConfig();

            if (_beingConfigJson != null)
            {
                _initialized = InitializeBeing(_beingConfigJson);

                if (_initialized && _AutoStartConversation)
                {
                    StartNewConversation(UserId).Forget();
                }
            }
        }

        private bool InitializeBeing(string configJson)
        {
            if (string.IsNullOrEmpty(configJson))
            {
                return false;
            }
            ApiBeingConfig = VirbeUtils.ParseConfig(configJson);
            if (ApiBeingConfig == null)
            {
                return false;
            }
            if(_baseUrl == null)
            {
                Debug.LogError($"Base Url is null, ensure proper Api URL in {nameof(VirbeBeing)}settings.");
                return false;
            }
            if (_ProfileID == null)
            {
                Debug.LogError($"Profile ID is null, ensure proper Profile ID in {nameof(VirbeBeing)}settings.");
                return false;
            }
            if (_ProfileSecret == null)
            {
                Debug.LogError($"Profile Secret is null, ensure proper Profile Secret in {nameof(VirbeBeing)}settings.");
                return false;
            }
            if (_appIdentifer == null)
            {
                Debug.LogError($"App identifier is null, ensure proper App identifier in Player settings.");
                return false;
            }
            _communicationSystem = new CommunicationSystem(this, _baseUrl, _ProfileID, _ProfileSecret, _appIdentifer);
            _communicationSystem.UserActionExecuted += CallUserAction;
            _communicationSystem.BeingActionExecuted += (args) => _virbeActionPlayer.ScheduleNewAction(args);
            _communicationSystem.UserSpeechRecognized += CallUserSpeechRecognized;
            _communicationSystem.UiActionExecuted += CallUiAction;
            _communicationSystem.CustomActionExecuted += CallCustomAction;
            _communicationSystem.BehaviourActionExecuted += CallbehaviourAction;
            _communicationSystem.EngineEventExecuted += CallEngineEvent;
            _communicationSystem.SignalExecuted += CallSignal;
            _communicationSystem.NamedActionExecuted += CallNamedAction;
            _communicationSystem.ConversationInitialized += OnConversationInitialized;
            return true;
        }

        private void OnConversationInitialized(string conversationId)
        {
            ConversationId = Guid.Parse(conversationId);
            if (_AutoStartConversation)
            {
                _communicationSystem.SendSignal("conversation-start", string.Empty).Forget();
            }
        }

        private async UniTask RestoreConversation(Guid endUserId, Guid conversationId)
        {
            if (!_initialized)
            {
                _logger.Log("Being not initialized");
                return;
            }
            if (_PersistentUserID)
            {
                PlayerPrefs.SetString(_userIdKey, endUserId.ToString());
            }
            if (_PersistentConversationID)
            {
                PlayerPrefs.SetString(_conversationIdKey, conversationId.ToString());
            }
            UserId = endUserId;
            ConversationId = conversationId;

            await _communicationSystem.InitializeWith(endUserId, conversationId);
            ConversationStarted?.Invoke();
        }

        private bool CanChangeBeingState(Behaviour newBehaviour)
        {
            Behaviour[] allowedCurrentStates;
            switch (newBehaviour)
            {
                case Behaviour.Idle:
                    allowedCurrentStates = new[] { Behaviour.Focused };
                    break;
                case Behaviour.Focused:
                    allowedCurrentStates = new[] { Behaviour.Idle, Behaviour.InConversation };
                    break;
                case Behaviour.InConversation:
                    allowedCurrentStates = new[]
                    {
                        Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation, Behaviour.Listening,
                        Behaviour.PlayingBeingAction, Behaviour.RequestError
                    };
                    break;
                case Behaviour.Listening:
                    allowedCurrentStates = new[] { Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation };
                    break;
                case Behaviour.RequestProcessing:
                    allowedCurrentStates = new[]
                    {
                        Behaviour.Focused, Behaviour.Idle, Behaviour.InConversation, Behaviour.Listening,
                        Behaviour.RequestError
                    };
                    break;
                case Behaviour.RequestReceived:
                    allowedCurrentStates = new[] { Behaviour.RequestProcessing };
                    break;
                case Behaviour.RequestError:
                    allowedCurrentStates = new[] { Behaviour.RequestProcessing };
                    break;
                case Behaviour.PlayingBeingAction:
                    allowedCurrentStates = new[] { Behaviour.RequestReceived };
                    break;
                default:
                    return false;
            }

            return allowedCurrentStates.Contains(_currentState._behaviour);
        }
      
        private void ChangeBeingState(Behaviour newBehaviour)
        {
            if (_currentState._behaviour != newBehaviour)
            {
                _logger.Log($"Changing from state: {_currentState._behaviour} to {newBehaviour}");

                _currentState._behaviour = newBehaviour;
                onBeingStateChange?.Invoke(_currentState);
                OnBeingStateChanged?.Invoke(_currentState);
            }

            ScheduleChangeBeingStateAfterTimeoutIfNeeded();
        }

        private void ScheduleChangeBeingStateAfterTimeoutIfNeeded()
        {
            //safeguard if being is already diabled
            if (!enabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            if (_autoBeingStateChangeCoroutine != null)
            {
                // Canceling previous timeout to avoid unexpected changes
                StopCoroutine(_autoBeingStateChangeCoroutine);
            }

            switch (_currentState._behaviour)
            {
                case Behaviour.Listening:
                    // make sure being is not constantly waiting for recording
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(listeningStateTimeout, Behaviour.InConversation));
                    break;
                case Behaviour.InConversation:
                    // make sure being is not constantly waiting for recording
                    // Next state: InConversation
                    _autoBeingStateChangeCoroutine =
                          StartCoroutine(WaitAndChangeToState(inConversationStateTimeout, Behaviour.Focused));
                    break;
                case Behaviour.RequestError:
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(requestErrorStateTimeout, Behaviour.InConversation));
                    break;
                case Behaviour.Focused:
                    // make sure being is not constantly focused on user after conversation
                    // Next state: Idle
                    _autoBeingStateChangeCoroutine =
                        StartCoroutine(WaitAndChangeToState(focusedStateTimeout, Behaviour.Idle));
                    break;
            }
        }

        private IEnumerator WaitAndChangeToState(float timeout, Behaviour newBehaviour)
        {
            yield return new WaitForSeconds(timeout);
            if (gameObject != null)
            {
                ChangeBeingState(newBehaviour);
            }
        }

        internal void CallBeingActionStarted(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.PlayingBeingAction);

            _currentState.lastBeingAction = beingAction;
            OnBeingAction?.Invoke(beingAction);
            onBeingAction?.Invoke(beingAction);
        }

        internal void CallBeingActionEnded(BeingAction beingAction)
        {
            ChangeBeingState(Behaviour.InConversation);
        }

        private void CallUserSpeechRecognized(string text)
        {
            UserSpeechRecognized?.Invoke(text);
            userSpeechRecognized?.Invoke(text);
        }

        private void CallUserAction(UserAction userAction)
        {
            _currentState.lastUserAction = userAction;
            OnUserAction?.Invoke(userAction);
            onUserAction?.Invoke(userAction);
        }
        private void CallNamedAction(NamedAction action)
        {
            OnNamedAction?.Invoke(action);
            onNamedAction?.Invoke(action);
        }

        private void CallSignal(Signal signal)
        {
            OnSignal?.Invoke(signal);
            onSignal?.Invoke(signal);
        }

        private void CallEngineEvent(EngineEvent @event)
        {
            OnEngineEvent?.Invoke(@event);
            onEngineEvent?.Invoke(@event);
        }

        private void CallbehaviourAction(VirbeBehaviorAction action)
        {
            OnBehaviourAction?.Invoke(action);
            onBehaviourAction?.Invoke(action);
        }

        private void CallCustomAction(CustomAction action)
        {
            OnCustomAction?.Invoke(action);
            onCustomAction?.Invoke(action);
        }

        private void CallUiAction(VirbeUiAction action)
        {
            OnUiAction?.Invoke(action);
            onUiAction?.Invoke(action);
        }
    }
}