using Plugin.InAppBilling;
using Plugin.InAppBilling.Abstractions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RedCorners
{
    public class IAP
    {
        public event EventHandler<InAppBillingPurchase> OnPurchase;

        public async Task<IEnumerable<InAppBillingProduct>> GetAvailableProductsAsync(ItemType itemType, params string[] productIds)
        {
            if (!CrossInAppBilling.IsSupported)
                return null;

            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync(itemType);
                if (!connected) return null;
                return await billing.GetProductInfoAsync(itemType, productIds);

            }
            finally
            {
                await billing.DisconnectAsync();
            }
        }

        public async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType)
        {
            if (!CrossInAppBilling.IsSupported)
                return null;

            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync();
                if (!connected) return null;
                return await billing.GetPurchasesAsync(itemType, new Verify());

            }
            finally
            {
                await billing.DisconnectAsync();
            }
        }

        volatile bool isPurchasing = false;


        public async Task<InAppBillingPurchase> PurchaseItemAsync(ItemType itemType, string productId)
        {
            return await PurchaseItemAsync(itemType, productId, productId);
        }

        public async Task<InAppBillingPurchase> PurchaseItemAsync(ItemType itemType, string productId, string payload)
        {
            if (isPurchasing) return null;
            isPurchasing = true;
            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync(itemType);
                if (!connected)
                {
                    //we are offline or can't connect, don't try to purchase
                    Debug.WriteLine("PurchaseItem: Not Connected");
                    isPurchasing = false;
                    return null;
                }
                //restore purchases
                try
                {
                    var existing = (await billing.GetPurchasesAsync(itemType, new Verify())).ToList();
                    if (existing != null)
                    {
                        var item = existing.FirstOrDefault(x => x.ProductId == productId);
                        if (item != null &&
                            (item.State == PurchaseState.Purchased ||
                            item.State == PurchaseState.Restored))
                        {
                            isPurchasing = false;
                            return item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PurchaseItem: Existing error: {ex}");
                }

                //check purchases
                var purchase = await billing.PurchaseAsync(productId, itemType, payload, new Verify());

                //possibility that a null came through.
                if (purchase == null)
                {
                    //did not purchase
                    Debug.WriteLine("PurchaseItem: Did not purchase");
                }
                else if (purchase.State == PurchaseState.Purchased)
                {
                    //purchased!
                    OnPurchase?.Invoke(this, purchase);

                    Debug.WriteLine("PurchaseItem: True");
                    return purchase;
                }
            }
            catch (InAppBillingPurchaseException purchaseEx)
            {
                //Billing Exception handle this based on the type
                Debug.WriteLine("Purchase Error: " + purchaseEx);
            }
            catch (Exception ex)
            {
                //Something else has gone wrong, log it
                Debug.WriteLine("Issue connecting: " + ex);
            }
            finally
            {
                await billing.DisconnectAsync();
                isPurchasing = false;
            }

            Debug.WriteLine("PurchaseItem: False");
            return null;
        }

        internal class Verify : IInAppBillingVerifyPurchase
        {
            //const string key1 = @"XOR_key1";
            //const string key2 = @"XOR_key2";
            //const string key3 = @"XOR_key3";

            public Task<bool> VerifyPurchase(string signedData, string signature, string productId = null, string transactionId = null)
            {
                return Task.FromResult(true);

                //#if __ANDROID__
                //            var key1Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key1, 1);
                //            var key2Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key2, 2);
                //            var key3Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key3, 3);

                //            return Task.FromResult(Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.VerifyPurchase(key1Transform + key2Transform + key3Transform, signedData, signature));
                //#else
                //            return Task.FromResult(true);
                //#endif
            }
        }
    }
}
