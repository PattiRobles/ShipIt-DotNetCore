
namespace ShipIt.Models.ApiModels
{
    public class OutboundOrderResponse : Response
    {
        public Product Product { get; set; }
        public string Error { get; set; }

        public OutboundOrderResponse()
        {
            Success = false;
        }
    }
}