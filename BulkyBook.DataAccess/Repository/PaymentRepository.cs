using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models.PaystackModels;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PayStack.Net;
using BulkyBook.Models.ViewModels;
using BulkyBook.Models;

namespace BulkyBook.DataAccess.Repository
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IPaymentRepository _paymentRepository;
        private PayStackApi _payStackApi;
        private readonly string _secretKey;

        public PaymentRepository(IUnitOfWork unitOfWork, ApplicationDbContext db, IConfiguration configuration, 
            IPaymentRepository paymentRepository, string secretKey)
        {
            _unitOfWork = unitOfWork;
            _db = db;
            _configuration = configuration;
            _paymentRepository = paymentRepository;
            _secretKey = _configuration["Paystack:SecretKey"];
            _payStackApi = new PayStackApi(secretKey);
            
        }

        public async Task<InitiateResponse> InitializePayment(PaystackInitiateRequest request)
        {
            try
            {
                InitiateResponse response = new InitiateResponse();
                TransactionInitializeRequest paystackRequest = new TransactionInitializeRequest
                {
                    AmountInKobo = int.Parse(request.Amount) * 100,
                    Email = request.Email,
                    Currency = "ZAR",
                    CallbackUrl = _configuration["Paystack:CallBackUrl"],
                };
                TransactionInitializeResponse responseFromPaystack = _payStackApi.Transactions.Initialize(paystackRequest);

                if(responseFromPaystack.Status)
                {
                    Payment payment = new Payment
                    {
                        Amount = decimal.Parse(request.Amount),
                        Email = request.Email,
                        Currency = "ZAR",
                        Status = "pending",
                        Reference = responseFromPaystack.Data.Reference
                    };

                    await _db.AddAsync(payment);
                    await _db.SaveChangesAsync();
                }
                response = new InitiateResponse
                {
                    Status = responseFromPaystack.Status,
                    Message = responseFromPaystack.Message,
                    AuthorizationUrl = responseFromPaystack.Data.AuthorizationUrl,
                    AccessCode = responseFromPaystack.Data.AccessCode,
                    Reference = responseFromPaystack.Data.Reference
                };
                return response;
            }
            catch (Exception)
            {
                var response = new InitiateResponse
                {
                    Status = false,
                    Message = "An error occured while initializing payment"
                };
                return response;
            }
        }

        public async Task<PaystackVerifyResponse> VerifyPayment(string reference)
        {
            try
            {
                PaystackVerifyResponse response = new PaystackVerifyResponse();
                TransactionVerifyResponse verifyResponse = _payStackApi.Transactions.Verify(reference);

                if(verifyResponse.Status)
                {
                    var updateDatabase = new Payment
                    {
                        Status = "success",
                        Message = verifyResponse.Message,
                        PaymentMethod = verifyResponse.Data.Channel,
                        Banks = verifyResponse.Data.Authorization.Bank,
                        PaymentDate = verifyResponse.Data.TransactionDate
                    };
                    _db.Update(updateDatabase);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    var updateDatabase = new Payment
                    {
                        Status = "failed",
                        Message = verifyResponse.Message,
                        PaymentMethod = verifyResponse.Data.Channel,
                        Banks = verifyResponse.Data.Authorization.Bank,
                        PaymentDate = verifyResponse.Data.TransactionDate
                    };
                    _db.Update(updateDatabase);
                    await _db.SaveChangesAsync();
                }
                response = new PaystackVerifyResponse
                {
                    Status = verifyResponse.Status,
                    Message = verifyResponse.Message,
                };
                return response;
            }
            catch (Exception)
            {

                var response = new PaystackVerifyResponse
                {
                    Status = false,
                    Message = "Error occured while verifying payment"
                };
                return response;
            }
            
        }

        public void Update(Payment obj)
        {
            throw new NotImplementedException();
        }
    }
}
