using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BulkyBook.Models.PaystackModels
{
    public class PaystackInitiateRequest
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [Required]
        [JsonPropertyName("amount")]
        [Range(0, 100000000, ErrorMessage ="Amount must be between {1} and {2}")]
        public string Amount { get; set; }
    }
}
