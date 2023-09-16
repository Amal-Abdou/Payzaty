using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Payments.Payzaty.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Payzaty.Components
{
    [ViewComponent(Name = "PaymentPayzaty")]
    public class PaymentPayzatyViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel()
            {
                CreditCardTypes = new List<SelectListItem>
                {
                    new SelectListItem { Text = "Master Card", Value = "mastercard" },
                    new SelectListItem { Text = "Credit Card", Value = "creditcard" },
                }
            };
            return View("~/Plugins/Payments.Payzaty/Views/PaymentInfo.cshtml", model);
        }
    }
}
