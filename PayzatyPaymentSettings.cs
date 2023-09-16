using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Payzaty
{
    public class PayzatyPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }

        public string AccountNo { get; set; }

        public string SecretKey { get; set; }

    }
}
