namespace SIS.Models.Payments
{
    public class FeeBreakdownItem
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
