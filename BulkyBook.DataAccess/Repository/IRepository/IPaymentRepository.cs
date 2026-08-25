using BulkyBook.Models.PaystackModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IPaymentRepository
    {
        void Update(Payment obj);
        public Task<InitiateResponse> InitializePayment(PaystackInitiateRequest request);
        public Task<PaystackVerifyResponse> VerifyPayment(string reference);
    }
}
