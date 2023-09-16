using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Payzaty.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //Success
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Payzaty.Success", "Plugins/PaymentPayzaty/Success",
                 new { controller = "PaymentPayzaty", action = "Success" });


            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Payzaty.Cancel", "Plugins/PaymentPayzaty/Cancel",
                 new { controller = "PaymentPayzaty", action = "Cancel" });
        }

        public int Priority => -1;
    }
}