using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class CloudCodeManager : MonoBehaviour
        {
            // Cloud Code SDK status codes from Client
            const int k_CloudCodeRateLimitExceptionStatusCode = 50;
            const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
            const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

            // HTTP REST API status codes
            const int k_HttpBadRequestStatusCode = 400;
            const int k_HttpTooManyRequestsStatusCode = 429;

            // Custom status codes
            const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
            const int k_VirtualPurchaseFailedStatusCode = 2;
            
            // Unity Gaming Services status codes via Cloud Code
            const int k_EconomyPurchaseCostsNotMetStatusCode = 10504;
            const int k_EconomyValidationExceptionStatusCode = 1007;
            const int k_RateLimitExceptionStatusCode = 50;

            public static CloudCodeManager instance { get; private set; }

            public BattlePassSampleView sceneView;

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            public async Task<GetStateResult> CallGetProgressEndpoint()
            {
                try
                {
                    Debug.Log("Getting current Battle Pass progress via Cloud Code...");

                    // The CallEndpointAsync method requires two objects to be passed in: the name of the script being
                    // called, and a struct for any arguments that need to be passed to the script. In this sample,
                    // we didn't need to pass any additional arguments, so we're passing an empty string. You could
                    // pass an empty struct. See CallGainSeasonXpEndpoint for an example with non-empty args.

                    return await CloudCodeService.Instance.CallEndpointAsync<GetStateResult>(
                        "BattlePass_GetState",
                        new Dictionary<string, object>());
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallGetProgressEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<GainSeasonXpResult> CallGainSeasonXpEndpoint(int xpToGain)
            {
                try
                {
                    Debug.Log("Gaining Season XP via Cloud Code...");

                    return await CloudCodeService.Instance.CallEndpointAsync<GainSeasonXpResult>(
                        "BattlePass_GainSeasonXP",
                        new Dictionary<string, object> {{ "amount", xpToGain }});
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallGainSeasonXpEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<PurchaseBattlePassResult> CallPurchaseBattlePassEndpoint()
            {
                try
                {
                    Debug.Log("Purchasing the current Battle Pass via Cloud Code...");

                    return await CloudCodeService.Instance.CallEndpointAsync<PurchaseBattlePassResult>(
                        "BattlePass_PurchaseBattlePass",
                        new Dictionary<string, object>());
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallPurchaseBattlePassEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<ClaimTierResult> CallClaimTierEndpoint(int tierIndexToClaim)
            {
                try
                {
                    Debug.Log($"Claiming tier {tierIndexToClaim + 1} via Cloud Code...");

                    return await CloudCodeService.Instance.CallEndpointAsync<ClaimTierResult>(
                        "BattlePass_ClaimTier",
                        new Dictionary<string, object> {{ "tierIndex", tierIndexToClaim }});
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallClaimTierEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            void HandleCloudCodeException(CloudCodeException e)
            {
                switch (e.ErrorCode)
                {
                    case k_CloudCodeUnprocessableEntityExceptionStatusCode:
                        var cloudCodeCustomError = ConvertToActionableError(e);
                        HandleCloudCodeScriptError(cloudCodeCustomError);
                        break;

                    case k_CloudCodeRateLimitExceptionStatusCode:
                        Debug.Log("Rate Limit Exceeded. Try Again.");
                        break;

                    case k_CloudCodeMissingScriptExceptionStatusCode:
                        Debug.Log("Couldn't find requested Cloud Code Script");
                        break;

                    default:
                        Debug.Log(e);
                        break;
                }
            }

            static CloudCodeCustomError ConvertToActionableError(CloudCodeException e)
            {
                try
                {
                    // extract the JSON part of the exception message
                    var trimmedMessage = e.Message;
                    trimmedMessage = trimmedMessage.Substring(trimmedMessage.IndexOf('{'));
                    trimmedMessage = trimmedMessage.Substring(0, trimmedMessage.LastIndexOf('}') + 1);

                    // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                    // standard structure for all errors.
                    return JsonUtility.FromJson<CloudCodeCustomError>(trimmedMessage);
                }
                catch (Exception exception)
                {
                    return new CloudCodeCustomError("Failed to Parse Error", k_UnexpectedFormatCustomStatusCode,
                        "Cloud Code Unprocessable Entity exception is in an unexpected format and " +
                        $"couldn't be parsed: {exception.Message}", e);
                }
            }

            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_EconomyPurchaseCostsNotMetStatusCode:
                        sceneView.ShowCantAffordBattlePassPopup();
                        break;
                    
                    case k_VirtualPurchaseFailedStatusCode:
                        Debug.Log($"The purchase could not be completed: {cloudCodeCustomError.name}: " +
                                  $"{cloudCodeCustomError.message}");
                        break;

                    case k_EconomyValidationExceptionStatusCode:
                    case k_HttpBadRequestStatusCode:
                        Debug.Log("A bad server request occurred during Cloud Code script execution: " +
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.details[0]}");
                        break;

                    case k_RateLimitExceptionStatusCode:
                        // With this status code, message will include which service triggered this rate limit.
                        Debug.Log($"{cloudCodeCustomError.message}. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    case k_HttpTooManyRequestsStatusCode:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    case k_UnexpectedFormatCustomStatusCode:
                        Debug.Log($"Cloud Code returned an Unprocessable Entity exception, " +
                                  $"but it could not be parsed: { cloudCodeCustomError.message }. " +
                                  $"Original error: { cloudCodeCustomError.InnerException?.Message }");
                        break;

                    default:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message}");
                        break;
                }
            }

            public struct ResultReward
            {
                public string service;
                public string id;
                public int quantity;
                public string spriteAddress;
            }

            public struct GetStateResult
            {
                public int seasonXp;
                public bool ownsBattlePass;
                public int[] seasonTierStates;
                public int eventSecondsRemaining;
                public GetStateRemoteConfigs remoteConfigs;
            }

            public struct GetStateRemoteConfigs
            {
                public ResultReward[] battlePassRewardsFree;
                public ResultReward[] battlePassRewardsPremium;
                public int battlePassSeasonXpPerTier;
                public string eventName;
            }

            public struct GainSeasonXpRequest
            {
                public int amount;
            }

            public struct GainSeasonXpResult
            {
                public int seasonXp;
                public int unlockedNewTier;
                public string validationResult;
                public int[] seasonTierStates;
            }

            public struct PurchaseBattlePassResult
            {
                public string purchaseResult;
                public ResultReward[] grantedRewards;
                public int[] seasonTierStates;
            }

            public struct ClaimTierResult
            {
                public string validationResult;
                public ResultReward[] grantedRewards;
                public int[] seasonTierStates;
            }

            struct CloudCodeExceptionParsedMessage
            {
                public string message;
            }

            class CloudCodeCustomError : Exception
            {
                public int status;
                public string name;
                public string message;
                public string retryAfter;
                public string[] details;

                public CloudCodeCustomError(string name, int status, string message = null, 
                    Exception innerException = null) : base(message, innerException)
                {
                    this.name = name;
                    this.status = status;
                    this.message = message;
                    retryAfter = null;
                    details = new string[] { };
                }
            }
        }
    }
}
