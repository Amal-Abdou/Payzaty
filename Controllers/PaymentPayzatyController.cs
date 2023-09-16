using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Payzaty.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Payzaty.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentPayzatyController : BasePaymentController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly PayzatyPaymentSettings _payzatyPaymentSettings;


        #endregion

        #region Ctor

        public PaymentPayzatyController(IGenericAttributeService genericAttributeService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            ShoppingCartSettings shoppingCartSettings,
            PayzatyPaymentSettings payzatyPaymentSettings)
        {
            _genericAttributeService = genericAttributeService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _shoppingCartSettings = shoppingCartSettings;
            _payzatyPaymentSettings = payzatyPaymentSettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var payzatyPaymentSettings = await _settingService.LoadSettingAsync<PayzatyPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = payzatyPaymentSettings.UseSandbox,
                AccountNo = payzatyPaymentSettings.AccountNo,
                SecretKey = payzatyPaymentSettings.SecretKey,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope <= 0)
                return View("~/Plugins/Payments.Payzaty/Views/Configure.cshtml", model);

            model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(payzatyPaymentSettings, x => x.UseSandbox, storeScope);
            model.AccountNo_OverrideForStore = await _settingService.SettingExistsAsync(payzatyPaymentSettings, x => x.AccountNo, storeScope);
            model.SecretKey_OverrideForStore = await _settingService.SettingExistsAsync(payzatyPaymentSettings, x => x.SecretKey, storeScope);
            
            return View("~/Plugins/Payments.Payzaty/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]        
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var payzatyPaymentSettings = await _settingService.LoadSettingAsync<PayzatyPaymentSettings>(storeScope);

            payzatyPaymentSettings.UseSandbox = model.UseSandbox;
            payzatyPaymentSettings.AccountNo = model.AccountNo;
            payzatyPaymentSettings.SecretKey = model.SecretKey;

            await _settingService.SaveSettingOverridablePerStoreAsync(payzatyPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(payzatyPaymentSettings, x => x.AccountNo, model.AccountNo_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(payzatyPaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate PayPal rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = await _localizationService.GetResourceAsync("Plugins.Payments.Payzaty.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }

        public async Task<IActionResult> Success(string checkoutId)
        {

            var baseUrl = _payzatyPaymentSettings.UseSandbox == true ? "https://api.sandbox.payzaty.com/checkout/" : "https://api.payzaty.com/checkout/";
            baseUrl = baseUrl + checkoutId;
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-AccountNo", _payzatyPaymentSettings.AccountNo);
            client.DefaultRequestHeaders.Add("X-SecretKey", _payzatyPaymentSettings.SecretKey);
            var response = client.GetAsync(baseUrl).Result;

            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!string.IsNullOrEmpty(responseString) && JObject.Parse(responseString).GetValue("paid") != null )
            {
                
                var reference = JObject.Parse(responseString).GetValue("reference").ToString();

                var order = await _orderService.GetOrderByIdAsync(int.Parse(reference));

                    if (order != null && JObject.Parse(responseString).GetValue("paid").ToString() == "True")
                    {
                        await _orderProcessingService.MarkOrderAsPaidAsync(order);
                        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                    else if (order != null && JObject.Parse(responseString).GetValue("paid").ToString() == "False")
                    {
                        await _orderService.InsertOrderNoteAsync(new OrderNote
                        {
                            OrderId = order.Id,
                            Note = " The Payment failed , Please try again from your account in order details page.",
                            DisplayToCustomer = true,
                            CreatedOnUtc = DateTime.UtcNow
                        });
                        await _orderService.UpdateOrderAsync(order);
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                }

            }

            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }

        public async Task<IActionResult> Cancel(string checkoutId)
        {

            var baseUrl = _payzatyPaymentSettings.UseSandbox == true ? "https://api.sandbox.payzaty.com/checkout/" : "https://api.payzaty.com/checkout/";
            baseUrl = baseUrl + checkoutId;
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-AccountNo", _payzatyPaymentSettings.AccountNo);
            client.DefaultRequestHeaders.Add("X-SecretKey", _payzatyPaymentSettings.SecretKey);
            var response = client.GetAsync(baseUrl).Result;

            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!string.IsNullOrEmpty(responseString) && JObject.Parse(responseString).GetValue("paid") != null)
            {

                var reference = JObject.Parse(responseString).GetValue("reference").ToString();

                var order = await _orderService.GetOrderByIdAsync(int.Parse(reference));

                if (order != null && JObject.Parse(responseString).GetValue("paid").ToString() == "True")
                {
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
                else if (order != null && JObject.Parse(responseString).GetValue("paid").ToString() == "False")
                {
                    var orderNote= new OrderNote
                    {
                        OrderId = order.Id,
                        Note = " The Payment failed , Please try again from your account in order details page.",
                        DisplayToCustomer = true,
                        CreatedOnUtc = DateTime.UtcNow
                    };
                    var orderNotes=await _orderService.GetOrderNotesByOrderIdAsync(order.Id);
                    if(orderNotes != null && orderNotes.Count > 0)
                    {
                        if (orderNotes.Select(x => x.Note).Contains(orderNote.Note) == false)
                        {
                            await _orderService.InsertOrderNoteAsync(orderNote);
                            await _orderService.UpdateOrderAsync(order);
                        }
                    }
                    else
                    {
                        await _orderService.InsertOrderNoteAsync(orderNote);
                        await _orderService.UpdateOrderAsync(order);
                    }
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

                }


            }

            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }

        #endregion
    }
}