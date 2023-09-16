using System;
using FluentValidation;
using Nop.Plugin.Payments.Payzaty.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Payzaty.Validators
{
    public partial class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {

            RuleFor(x => x.CreditCardType).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Payment.CreditCardType.Required"));

        }
    }
}