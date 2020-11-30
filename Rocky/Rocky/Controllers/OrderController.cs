using Braintree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Rocky_DataAccess.Repository.IRepository;
using Rocky_Models;
using Rocky_Models.ViewModels;
using Rocky_Utility;
using Rocky_Utility.BrainTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rocky.Controllers
{
    [Authorize(Roles = WC.AdminRole)]
    public class OrderController : Controller
    {
        private readonly IOrderHeaderRepository _orderHRepo;
        private readonly IOrderDetailsRepository _orderDRepo;
        private readonly IBrainTreeGate _brain;
        public OrderVM orderVM { get; set; }
        public OrderController(IOrderHeaderRepository orderHRepo, IOrderDetailsRepository orderDRepo, IBrainTreeGate brain)
        {
            _orderHRepo = orderHRepo;
            _orderDRepo = orderDRepo;
            _brain = brain;
        }
        public IActionResult Index(string searchName=null, string searchEmail=null, string searchPhone=null, string Status=null)
        {
            OrderListVM orderListVM = new OrderListVM()
            {
                OrderHList = _orderHRepo.GetAll(),
                StatusList = WC.listStatus.ToList().Select(i => new SelectListItem
                {
                    Text = i,
                    Value = i
                })
            };

            if (!string.IsNullOrEmpty(searchName))
            {
                orderListVM.OrderHList = orderListVM.OrderHList.Where(u => u.FullName.ToLower().Contains(searchName.ToLower()));
            }

            if (!string.IsNullOrEmpty(searchEmail))
            {
                orderListVM.OrderHList = orderListVM.OrderHList.Where(u => u.Email.ToLower().Contains(searchEmail.ToLower()));
            }

            if (!string.IsNullOrEmpty(searchPhone))
            {
                orderListVM.OrderHList = orderListVM.OrderHList.Where(u => u.PhoneNumber.ToLower().Contains(searchPhone.ToLower()));
            }

            if (!string.IsNullOrEmpty(Status) && Status != "--Order Status--")
            {
                orderListVM.OrderHList = orderListVM.OrderHList.Where(u => u.OrderStatus.ToLower().Contains(Status.ToLower()));
            }

            return View(orderListVM);
        }
        public IActionResult Details(int id)
        {
            orderVM = new OrderVM()
            {
                OrderHeader = _orderHRepo.FirstOrDefault(u => u.Id == id),
                OrderDetail = _orderDRepo.GetAll(o => o.OrderHeaderId == id, includeProperties: "Product")
            };
            return View(orderVM);
        }

        [HttpPost]
        public IActionResult StartProcessing()
        {
            OrderHeader orderHeader = _orderHRepo.FirstOrDefault(u=>u.Id == orderVM.OrderHeader.Id);
            orderHeader.OrderStatus = WC.StatusInProcess;
            _orderHRepo.Save();
            TempData[WC.Success] = "Order Shipped Successfully";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult ShipOrder()
        {
            OrderHeader orderHeader = _orderHRepo.FirstOrDefault(u => u.Id == orderVM.OrderHeader.Id);
            orderHeader.OrderStatus = WC.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;
            _orderHRepo.Save();
            TempData[WC.Success] = "Order Shipped Successfully";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult CancelOrder()
        {
            OrderHeader orderHeader = _orderHRepo.FirstOrDefault(u => u.Id == orderVM.OrderHeader.Id);

            var gateway = _brain.GetGateway();
            Transaction transaction = gateway.Transaction.Find(orderHeader.TransactionId);

            if (transaction.Status == TransactionStatus.AUTHORIZED || transaction.Status == TransactionStatus.SUBMITTED_FOR_SETTLEMENT)
            {
                //no refund
                Result<Transaction> resultVoid = gateway.Transaction.Void(orderHeader.TransactionId);
            }
            else
            {
                //refund
                Result<Transaction> resultRefund = gateway.Transaction.Refund(orderHeader.TransactionId);
            }

            orderHeader.OrderStatus = WC.StatusRefunded;
            TempData[WC.Success] = "Order Cancelled Successfully";
            _orderHRepo.Save();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UpdateOrderDetails()
        {
            OrderHeader orderHeaderFromDB = _orderHRepo.FirstOrDefault(u => u.Id == orderVM.OrderHeader.Id);
            orderHeaderFromDB.FullName = orderVM.OrderHeader.FullName;
            orderHeaderFromDB.PhoneNumber = orderVM.OrderHeader.PhoneNumber;
            orderHeaderFromDB.StreetAddress = orderVM.OrderHeader.StreetAddress;
            orderHeaderFromDB.City = orderVM.OrderHeader.City;
            orderHeaderFromDB.State = orderVM.OrderHeader.State;
            orderHeaderFromDB.PostalCode = orderVM.OrderHeader.PostalCode;
            orderHeaderFromDB.Email = orderVM.OrderHeader.Email;
            _orderHRepo.Save();
            TempData[WC.Success] = "Order Details Updated Successfully";

            return RedirectToAction("Details", "Order", new { Id = orderHeaderFromDB.Id });
        }
    }
}
