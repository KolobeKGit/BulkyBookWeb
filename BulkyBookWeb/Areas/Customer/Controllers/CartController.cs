using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Stripe.BillingPortal;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;
using Session = Stripe.Checkout.Session;
using SessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using SessionService = Stripe.Checkout.SessionService;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHearder = new()
            };
            


            //Iterating through each product in the cart
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPricesBasedOnQuanity(cart);
                ShoppingCartVM.OrderHearder.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHearder = new()
            };

            //Populate the application user
            ShoppingCartVM.OrderHearder.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
            //Update properties in OrderHearder
            ShoppingCartVM.OrderHearder.Name = ShoppingCartVM.OrderHearder.ApplicationUser.Name;
            ShoppingCartVM.OrderHearder.PhoneNumber = ShoppingCartVM.OrderHearder.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHearder.StreetAddress = ShoppingCartVM.OrderHearder.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHearder.City = ShoppingCartVM.OrderHearder.ApplicationUser.City;
            ShoppingCartVM.OrderHearder.Province = ShoppingCartVM.OrderHearder.ApplicationUser.Province;
            ShoppingCartVM.OrderHearder.PostalCode = ShoppingCartVM.OrderHearder.ApplicationUser.PostalCode;

            //Iterating through each product in the cart
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPricesBasedOnQuanity(cart);
                ShoppingCartVM.OrderHearder.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }
        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product");

            ShoppingCartVM.OrderHearder.OrderDate = System.DateTime.Now;
            ShoppingCartVM.OrderHearder.ApplicationUserId = userId;

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);


            //Iterating through each product in the cart
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPricesBasedOnQuanity(cart);
                ShoppingCartVM.OrderHearder.OrderTotal += (cart.Price * cart.Count);
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //It is a regular customer account and we need to capture payment
                ShoppingCartVM.OrderHearder.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.OrderHearder.OrderStatus = SD.StatusPending;
            }
            else
            {
                //It is a company user
                ShoppingCartVM.OrderHearder.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.OrderHearder.OrderStatus = SD.StatusApproved;
            }
            _unitOfWork.OrderHearder.Add(ShoppingCartVM.OrderHearder);
            _unitOfWork.Save();

            //Creating OrderDetail record
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = cart.ProductId,
                    Count = cart.Count,
                    OrderHearderId = ShoppingCartVM.OrderHearder.Id,
                    Price = cart.Price,
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //It is a regular customer account and we need to capture payment
                //Stripe logic
                var domain = "https://localhost:44373/";
                var options = new SessionCreateOptions
                {
           
                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHearder.Id}",
                    CancelUrl = domain + "customer/cart/index",
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment"
                };

                //getting shopping items in the cart
                foreach (var item in ShoppingCartVM.ShoppingCartList)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100), //$20.50 => 2050
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Title
                            }
                        },
                        Quantity = item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }
                var service = new SessionService();
                Session session = service.Create(options);
                _unitOfWork.OrderHearder.updateStripePaymentID(ShoppingCartVM.OrderHearder.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                //url to redirect to after
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303); //new URL provided by stripe

            }

            return RedirectToAction(nameof(OrderConfirmation), new {id = ShoppingCartVM.OrderHearder.Id});
        }
        public IActionResult OrderConfirmation(int id)
        {
            OrderHearder orderHearder = _unitOfWork.OrderHearder.Get(u => u.Id == id, includeProperties: "ApplicationUser");
            if (orderHearder.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                //this is an order by customer
                var service = new SessionService();
                Session session = service.Get(orderHearder.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHearder.updateStripePaymentID(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHearder.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
                HttpContext.Session.Clear();
            }
            List<ShoppingCart> ShoppingCarts = _unitOfWork.ShoppingCart
                .GetAll(u => u.ApplicationUserId == orderHearder.ApplicationUserId).ToList();
            _unitOfWork.ShoppingCart.RemoveRange(ShoppingCarts);
            _unitOfWork.Save(); 

            return View(id);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
            if (cartFromDb.Count <= 1)
            {
                //Remove from cart
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
                .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
                .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);           
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        private double GetPricesBasedOnQuanity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}
