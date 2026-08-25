using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IOrderHearderRepository : IRepository<OrderHearder>
    {
        void Update(OrderHearder obj);
        void UpdateStatus(int id, string orderStatus, string? paymentStatus = null);
        void updateStripePaymentID(int id, string sessionId, string paymentIntentId);
    }
}
