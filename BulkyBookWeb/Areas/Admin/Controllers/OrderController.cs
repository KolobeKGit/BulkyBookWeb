using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("admin")]
    [Authorize]
    public class OrderController : Controller
    {
        
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public OrderVM OrderVM { get; set; }

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int orderId)
        {
            OrderVM= new()
            {
                OrderHearder = _unitOfWork.OrderHearder.Get(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderHearderId == orderId, includeProperties: "Product")
            };
            
            return View(OrderVM);
        }

        [HttpPost]
        [Authorize(Roles =SD.Role_Admin+","+SD.Role_Employee)]
        public IActionResult UpdateOrderDetail(int orderId)
        {
            var orderHearderFromDb = _unitOfWork.OrderHearder.Get(u => u.Id == OrderVM.OrderHearder.Id);
            orderHearderFromDb.Name = OrderVM.OrderHearder.Name;
            orderHearderFromDb.PhoneNumber = OrderVM.OrderHearder.PhoneNumber;
            orderHearderFromDb.StreetAddress = OrderVM.OrderHearder.StreetAddress;
            orderHearderFromDb.City = OrderVM.OrderHearder.City;
            orderHearderFromDb.Province = OrderVM.OrderHearder.Province;
            orderHearderFromDb.PostalCode = OrderVM.OrderHearder.PostalCode;
            if (!string.IsNullOrEmpty(OrderVM.OrderHearder.Carrier))
            {
                orderHearderFromDb.Carrier = OrderVM.OrderHearder.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderVM.OrderHearder.TrackingNumber))
            {
                orderHearderFromDb.TrackingNumber = OrderVM.OrderHearder.TrackingNumber;
            }
            
            _unitOfWork.OrderHearder.Update(orderHearderFromDb);
            _unitOfWork.Save();
            TempData["Success"] = "Order Details Updated Successfully";

            return RedirectToAction(nameof(Details), new { orderId = orderHearderFromDb.Id });
        }


        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult StartProcessing()
        {
            _unitOfWork.OrderHearder.UpdateStatus(OrderVM.OrderHearder.Id, SD.StatusInProcess);
            _unitOfWork.Save();
            TempData["Success"] = "Order Status Updated Successfully";

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHearder.Id });

        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult ShipOrder()
        {
            var orderHearderFromDb = _unitOfWork.OrderHearder.Get(u => u.Id == OrderVM.OrderHearder.Id);
            orderHearderFromDb.TrackingNumber = OrderVM.OrderHearder.TrackingNumber;
            orderHearderFromDb.Carrier = OrderVM.OrderHearder.Carrier;
            orderHearderFromDb.OrderStatus = SD.StatusShipped;
            orderHearderFromDb.ShippingDate = DateTime.Now;
            if (orderHearderFromDb.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                orderHearderFromDb.PAymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            }

            _unitOfWork.OrderHearder.Update(orderHearderFromDb);
            _unitOfWork.Save();
            TempData["Success"] = "Order Shipped Successfully";

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHearder.Id });

        }


        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult CancelOrder()
        {
            var orderHearderFromDb = _unitOfWork.OrderHearder.Get(u => u.Id == OrderVM.OrderHearder.Id);

            //There can only be a refund if the payment was apprived prior to the refund process
            if (orderHearderFromDb.PaymentStatus == SD.StatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHearderFromDb.PaymentIntentId
                };

                var service = new RefundService();
                Refund refund = service.Create(options);

                _unitOfWork.OrderHearder.UpdateStatus(orderHearderFromDb.Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else
            {
                _unitOfWork.OrderHearder.UpdateStatus(orderHearderFromDb.Id, SD.StatusCancelled, SD.StatusCancelled);
            }
            _unitOfWork.Save();
            TempData["Success"] = "Order Cancelled Successfully";

            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHearder.Id });
        }

        [ActionName("Details")]
        [HttpPost]
        public IActionResult Details_PAY_NOW()
        {
            OrderVM.OrderHearder = _unitOfWork.OrderHearder
                .Get(u => u.Id == OrderVM.OrderHearder.Id, includeProperties: "ApplicationUser");
            OrderVM.OrderDetails = _unitOfWork.OrderDetail
                .GetAll(u => u.OrderHearderId == OrderVM.OrderHearder.Id, includeProperties: "Product");

            //Stripe logic
            var domain = "https://localhost:44373/";
            var options = new SessionCreateOptions
            {

                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHearderId={OrderVM.OrderHearder.Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVM.OrderHearder.Id}",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment"
            };

            //getting shopping items in the cart
            foreach (var item in OrderVM.OrderDetails)
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
            _unitOfWork.OrderHearder.updateStripePaymentID(OrderVM.OrderHearder.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();
            //url to redirect to after
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303); //new URL provided by stripe
        }

        public IActionResult PaymentConfirmation(int orderHearderId)
        {
            OrderHearder orderHearder = _unitOfWork.OrderHearder.Get(u => u.Id == orderHearderId);
            if (orderHearder.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                //this is an order by a company
                var service = new SessionService();
                Session session = service.Get(orderHearder.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHearder.updateStripePaymentID(orderHearderId, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHearder.UpdateStatus(orderHearderId, orderHearder.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            }

            return View(orderHearderId);
        }

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHearder> objOrderHearders;

            //Validation for ensuring that only the admin and employee can access order page
            if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                objOrderHearders =  _unitOfWork.OrderHearder.GetAll(includeProperties: "ApplicationUser").ToList();
            }
            else
            {
                //Firstly we have to retrive the userid of the cutomer using claimsidentity
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

                objOrderHearders = _unitOfWork.OrderHearder
                    .GetAll(u => u.ApplicationUserId == userId, includeProperties: "ApplicationUser");

            }

            switch (status)
            {
                case "pending":
                    objOrderHearders = objOrderHearders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    objOrderHearders = objOrderHearders.Where(u => u.OrderStatus == SD.StatusInProcess);
                    break;
                case "completed":
                    objOrderHearders = objOrderHearders.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    objOrderHearders = objOrderHearders.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;
                case "declined":
                    objOrderHearders = objOrderHearders.Where(u => u.OrderStatus == SD.StatusCancelled);
                    break;
                default:
                    break;

            }
                    return Json(new { data = objOrderHearders });
        }

        #endregion
    }
}
