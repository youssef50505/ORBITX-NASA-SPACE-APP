using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace MarsSentinel.Bootstrap
{
    /// <summary>
    /// MarsServicesBootstrap manages initialization of Unity Gaming Services (UGS)
    /// and performs asynchronous anonymous player authentication.
    /// Adheres to Unity craft-game-floors non-blocking live floor design.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarsServicesBootstrap : MonoBehaviour
    {
        public static MarsServicesBootstrap Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Live Services State (Read-Only)")]
        [SerializeField] private bool isInitialized;
        [SerializeField] private bool isSignedIn;
        [SerializeField] private string playerId = string.Empty;

        public bool IsInitialized => isInitialized;
        public bool IsSignedIn => isSignedIn;
        public string PlayerId => playerId;

        public event Action<string> OnSignedIn;
        public event Action<string> OnSignInFailed;
        public event Action<Exception> OnInitializationFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private async void Start()
        {
            if (initializeOnStart)
            {
                await InitializeServicesAndSignInAsync();
            }
        }

        /// <summary>
        /// Asynchronously initializes Unity Services Core and signs in the player anonymously.
        /// Safe to call during startup without blocking the engine frame loop.
        /// </summary>
        public async Task<bool> InitializeServicesAndSignInAsync()
        {
            try
            {
                Debug.Log("[MarsServicesBootstrap] Initializing Unity Gaming Services Core...");

                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                isInitialized = (UnityServices.State == ServicesInitializationState.Initialized);
                Debug.Log($"[MarsServicesBootstrap] UGS Core Initialized: {isInitialized}");

                if (!isInitialized)
                {
                    Debug.LogWarning("[MarsServicesBootstrap] UGS Core state is not Initialized.");
                    return false;
                }

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    playerId = AuthenticationService.Instance.PlayerId;
                    isSignedIn = true;
                    Debug.Log($"[MarsServicesBootstrap] Already signed in with PlayerId: {playerId}");
                    OnSignedIn?.Invoke(playerId);
                    return true;
                }

                AuthenticationService.Instance.SignedIn += HandleSignedIn;
                AuthenticationService.Instance.SignInFailed += HandleSignInFailed;
                AuthenticationService.Instance.SignedOut += HandleSignedOut;

                Debug.Log("[MarsServicesBootstrap] Initiating anonymous sign-in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                return isSignedIn;
            }
            catch (AuthenticationException authEx)
            {
                Debug.LogWarning($"[MarsServicesBootstrap] AuthenticationException during anonymous sign-in: {authEx.ErrorCode} - {authEx.Message}");
                OnSignInFailed?.Invoke(authEx.Message);
                return false;
            }
            catch (RequestFailedException reqEx)
            {
                Debug.LogWarning($"[MarsServicesBootstrap] RequestFailedException during UGS initialization (offline mode): {reqEx.ErrorCode} - {reqEx.Message}");
                OnSignInFailed?.Invoke(reqEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MarsServicesBootstrap] Unexpected error during services bootstrap: {ex}");
                OnInitializationFailed?.Invoke(ex);
                return false;
            }
        }

        private void HandleSignedIn()
        {
            playerId = AuthenticationService.Instance.PlayerId;
            isSignedIn = true;
            Debug.Log($"[MarsServicesBootstrap] Successfully signed in anonymously. PlayerId: {playerId}");
            OnSignedIn?.Invoke(playerId);
        }

        private void HandleSignInFailed(RequestFailedException exception)
        {
            isSignedIn = false;
            Debug.LogWarning($"[MarsServicesBootstrap] Sign-in failed: {exception.ErrorCode} - {exception.Message}");
            OnSignInFailed?.Invoke(exception.Message);
        }

        private void HandleSignedOut()
        {
            playerId = string.Empty;
            isSignedIn = false;
            Debug.Log("[MarsServicesBootstrap] Player signed out.");
        }

        private void OnDestroy()
        {
            if (AuthenticationService.Instance != null)
            {
                AuthenticationService.Instance.SignedIn -= HandleSignedIn;
                AuthenticationService.Instance.SignInFailed -= HandleSignInFailed;
                AuthenticationService.Instance.SignedOut -= HandleSignedOut;
            }
        }
    }
}
