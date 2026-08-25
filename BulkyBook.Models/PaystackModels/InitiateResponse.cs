using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BulkyBook.Models.PaystackModels
{
    public class InitiateResponse
    {
        public int Id { get; set; }
        [JsonPropertyName("status")]
        public bool Status { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("authorization_url")]
        public string AuthorizationUrl { get; set; }
        [JsonPropertyName("access_code")]
        public string AccessCode { get; set; }
        [JsonPropertyName("reference")]
        public string Reference { get; set; }

    }
}
