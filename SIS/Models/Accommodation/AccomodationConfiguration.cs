using Microsoft.EntityFrameworkCore;

namespace SIS.Models.StudentAccommodation
{
    public class AccomodationConfiguration
    {
        public int Id {  get; set; }
        public string CreditCode { get; set; } = string.Empty;
        public string DebitCode { get; set; } = string.Empty;
        public int ReservationHoursValidity {  get; set; }
        [Precision(18,2)]
        public decimal AccommodationFee {  get; set; }
        public string LocationToTakeAccommodationPaymentReceipt { get; set; } = "Accommodation Office";
        public bool DeAllocateBedSpaceUponCheckOut {  get; set; }
    }
}
