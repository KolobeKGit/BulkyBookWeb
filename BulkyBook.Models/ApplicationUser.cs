using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Models
{
    public class ApplicationUser:IdentityUser
    {

        //If you modify anything in here, add migration and update database
        //Dont forget to change default project after opening Package Manager Console
        //Dont add Phone number because Identity has already added it
        [Required]
        public string Name { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
    }
}
