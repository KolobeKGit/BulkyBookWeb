using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Models.PaystackModels
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string Message { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public string Banks { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}
