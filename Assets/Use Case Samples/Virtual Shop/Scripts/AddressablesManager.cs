using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityGamingServicesUseCases
{
    namespace VirtualShop
    {
        public class AddressablesManager : MonoBehaviour
        {
            public static AddressablesManager instance { get; private set; }

            // Dictionary of Addressables Addresses to preloaded sprites.
            // Note: this dictionary just saves executing an async wait whenever a preloaded sprite is needed.
            public Dictionary<string, Sprite> preloadedSpritesByAddress { get; } =
                new Dictionary<string, Sprite>();

            // Dictionary of all economy items (Currencies and Items) to associated Sprite.
            // Note: this dictionary is a quick-n-dirty way to find the icon associated with any Currency
            //       or Inventory Item that has Custom Data correctly setup on the Economy Service.
            public Dictionary<string, Sprite> preloadedSpritesByEconomyId { get; } =
                new Dictionary<string, Sprite>();

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

            // Read all Currencies and Items to preload all associated Sprite Addresses from Custom data.
            public async Task PreloadAllEconomySprites()
            {
                var currenciesTask = Economy.Configuration.GetCurrenciesAsync();
                var itemsTask = Economy.Configuration.GetInventoryItemsAsync();
                await Task.WhenAll(currenciesTask, itemsTask);

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                // Setup 3 lists to facilitate async operation (within the AddressablesLoadAsyncData helper class).
                // Since we require a list of tasks to perform the await Task.WhenAll call, we can simply setup
                // the other 2 lists to track corresponding ids and sprite handles, which are required to
                // process results once all tasks are complete.
                var addressablesLoadAsyncData = new AddressablesLoadAsyncData();

                // Add all addressables for Currencies and Inventory Items to load-async data queue.
                AddAddressablesCurrencyTasks(currenciesTask.Result, addressablesLoadAsyncData);
                AddAddressablesItemTasks(itemsTask.Result, addressablesLoadAsyncData);

                // Wait for all Addressables to be loaded.
                await Task.WhenAll(addressablesLoadAsyncData.tasks);
                if (this == null) return;

                // Iterate all Addressables loaded and save off the Sprites into our Dictionary.
                AddAddressablesSpritesToDictionary(addressablesLoadAsyncData, preloadedSpritesByEconomyId);

                // TODO: remove these logs for epic pr
                Debug.Log("Economy sprites loaded:");
                foreach (var kvp in preloadedSpritesByEconomyId)
                {
                    Debug.Log($"{kvp.Key} => {kvp.Value}");
                }
            }

            // Parse all Shop Categories from Remote Config to preload all badge Addressables sprites.
            public async Task PreloadAllShopBadgeSprites(List<RemoteConfigManager.CategoryConfig> categories)
            {
                var addressablesLoadAsyncData = new AddressablesLoadAsyncData();

                // Add all Addressables Addresses from all Badge Icons listed in all Shop Items.
                foreach (var kvp in categories)
                {
                    foreach (var item in kvp.items)
                    {
                        var addressableAddress = item.badgeIconAddress;
                        addressablesLoadAsyncData.Add(addressableAddress);
                    }
                }

                // Wait for all Addressables to be loaded.
                await Task.WhenAll(addressablesLoadAsyncData.tasks);
                if (this == null) return;

                // Iterate all Addressables loaded and save off the Sprites into our Dictionary.
                AddAddressablesSpritesToDictionary(addressablesLoadAsyncData, preloadedSpritesByAddress);

                // TODO: remove these logs for epic pr
                Debug.Log("Preloaded sprites (currently only badges):");
                foreach (var kvp in preloadedSpritesByAddress)
                {
                    Debug.Log($"{kvp.Key} => {kvp.Value}");
                }
            }

            void AddAddressablesCurrencyTasks(List<CurrencyDefinition> currencyDefinitions,
                AddressablesLoadAsyncData addressablesLoadData)
            {
                foreach (var currencyDefinition in currencyDefinitions)
                {
                    var spriteAddress = currencyDefinition.CustomData["spriteAddress"] as string;
                    addressablesLoadData.Add(currencyDefinition.Id, spriteAddress);
                }
            }

            void AddAddressablesItemTasks(List<InventoryItemDefinition> inventoryItemDefinitions,
                AddressablesLoadAsyncData addressablesLoadData)
            {
                foreach (var inventoryItemDefinition in inventoryItemDefinitions)
                {
                    var spriteAddress = inventoryItemDefinition.CustomData["spriteAddress"] as string;
                    addressablesLoadData.Add(inventoryItemDefinition.Id, spriteAddress);
                }
            }

            void AddAddressablesSpritesToDictionary(AddressablesLoadAsyncData addressablesLoadData,
                Dictionary<string, Sprite> targetDictionary)
            {
                for (var i = 0; i < addressablesLoadData.ids.Count; i++)
                {
                    var id = addressablesLoadData.ids[i];
                    var handle = addressablesLoadData.handles[i];

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        targetDictionary[id] = handle.Result;
                    }
                    else
                    {
                        Debug.LogError($"A sprite could not be found for the address {id}." +
                            $" Addressables exception: {handle.OperationException}");
                    }
                }
            }

            List<ItemAndAmountSpec> ParseEconomyItems(List<PurchaseItemQuantity> itemQuantities)
            {
                var itemsAndAmountsSpec = new List<ItemAndAmountSpec>();

                foreach (var itemQuantity in itemQuantities)
                {
                    var id = itemQuantity.Item.GetReferencedConfigurationItem().Id;
                    itemsAndAmountsSpec.Add(new ItemAndAmountSpec(id, itemQuantity.Amount));
                }

                return itemsAndAmountsSpec;
            }
        }
    }
}
