using Plugin.InAppBilling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RedCorners.IAP
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
                var connected = await billing.ConnectAsync();
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
                return await billing.GetPurchasesAsync(itemType);

            }
            finally
            {
                await billing.DisconnectAsync();
            }
        }

        volatile bool isPurchasing = false;


        public async Task<InAppBillingPurchase> PurchaseItemAsync(ItemType itemType, string productId, bool restoreOnly)
        {
            if (isPurchasing) return null;
            isPurchasing = true;
            var billing = CrossInAppBilling.Current;
            try
            {
                var connected = await billing.ConnectAsync();
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
                    var existing = (await billing.GetPurchasesAsync(itemType)).ToList();
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

                if (restoreOnly)
                    return null;
                    
                //perform purchase
                var purchase = await billing.PurchaseAsync(productId, itemType);

                //possibility that a null came through.
                if (purchase == null)
                {
                    //did not purchase
                    Debug.WriteLine("PurchaseItem: Did not purchase");
                }
                else if (purchase.State == PurchaseState.Purchased)
                {
                    if (await billing.AcknowledgePurchaseAsync(purchase.PurchaseToken))
                    {
                        //purchased!
                        OnPurchase?.Invoke(this, purchase);

                        Debug.WriteLine("PurchaseItem: True");
                        return purchase;
                    }
                    else
                    {
                        Debug.WriteLine($"PurchaseItem: OK but didn't acknowledge!");
                    }
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
    }
}
