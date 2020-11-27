using Braintree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Rocky_DataAccess;
using Rocky_DataAccess.Repository.IRepository;
using Rocky_Models;
using Rocky_Models.ViewModels;
using Rocky_Utility;
using Rocky_Utility.BrainTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Rocky.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailSender _emailSender;
        private readonly IProductRepository _prodRepo;
        private readonly IApplicationUserRepository _appUserRepo;
        private readonly IInquiryHeaderRepository _inqHRepo;
        private readonly IInquiryDetailsRepository _inqDRepo;
        private readonly IOrderHeaderRepository _orderHRepo;
        private readonly IOrderDetailsRepository _orderDRepo;
        private readonly IBrainTreeGate _brain;

        [BindProperty]
        public ProductUserVM ProductUserVM { get; set; }
        public CartController(IWebHostEnvironment webHostEnvironment, IEmailSender emailSender, 
            IProductRepository prodRepo, IApplicationUserRepository appUserRepo,
            IInquiryHeaderRepository inqHRepo, IInquiryDetailsRepository inqDRepo,
            IOrderHeaderRepository orderHRepo, IOrderDetailsRepository orderDRepo,
            IBrainTreeGate brain)
        {
            _prodRepo = prodRepo;
            _appUserRepo = appUserRepo;
            _inqHRepo = inqHRepo;
            _inqDRepo = inqDRepo;
            _orderHRepo = orderHRepo;
            _orderDRepo = orderDRepo;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _brain = brain;
        }
        public IActionResult Index()
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if (HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null
                && HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }
            List<int> prodInCart = shoppingCartList.Select(u => u.ProductId).ToList();
            IEnumerable<Product> prodListTemp = _prodRepo.GetAll(u => prodInCart.Contains(u.Id));
            IList<Product> prodList = new List<Product>();
            foreach (var cartObj in shoppingCartList)
            {
                Product prodTemp = prodListTemp.FirstOrDefault(u => u.Id == cartObj.ProductId);
                prodTemp.TempSqFt = cartObj.SqFt;
                prodList.Add(prodTemp);
            }

            return View(prodList);
        }

        [HttpPost, ActionName("Index")]
        [ValidateAntiForgeryToken]
        public IActionResult IndexPost(IEnumerable<Product> prodList)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            foreach (var prod in prodList)
            {
                shoppingCartList.Add(new ShoppingCart { ProductId = prod.Id, SqFt = prod.TempSqFt });
            }
            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);
            return RedirectToAction(nameof(Summary));
        }

        public IActionResult Summary()
        {
            ApplicationUser appUser;
            if (User.IsInRole(WC.AdminRole))
            {
                if (HttpContext.Session.Get<int>(WC.SessionInquiryId) != 0)
                {
                    InquiryHeader inquiryHeader = _inqHRepo.FirstOrDefault(u => u.Id == HttpContext.Session.Get<int>(WC.SessionInquiryId));
                    appUser = new ApplicationUser()
                    {
                        Email = inquiryHeader.Email,
                        PhoneNumber = inquiryHeader.PhoneNumber,
                        FullName = inquiryHeader.FullName
                    };
                }
                else
                {
                    appUser = new ApplicationUser();
                }
                var gateway = _brain.GetGateway();
                var clientTocken = gateway.ClientToken.Generate();
                ViewBag.ClientToken = clientTocken;
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                //var userId = User.FindFirstValue(ClaimTypes.Name);
                appUser = _appUserRepo.FirstOrDefault(u => u.Id == claim.Value);
            }

            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if (HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null
                && HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }
            List<int> prodInCart = shoppingCartList.Select(u => u.ProductId).ToList();
            IEnumerable<Product> products = _prodRepo.GetAll(u => prodInCart.Contains(u.Id));

            ProductUserVM = new ProductUserVM()
            {
                ApplicationUser = appUser,
            };

            foreach (var cartObj in shoppingCartList)
            {
                Product prodTemp = _prodRepo.FirstOrDefault(u => u.Id == cartObj.ProductId);
                prodTemp.TempSqFt = cartObj.SqFt;
                ProductUserVM.ProductList.Add(prodTemp);
            }

            return View(ProductUserVM);
        }

        [HttpPost, ActionName("Summary")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SummaryPost(IFormCollection collection, ProductUserVM productUserVm)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (User.IsInRole(WC.AdminRole))
            {
                /* var orderTotal = 0.0;
                 foreach (var prod in productUserVm.ProductList)
                 {
                     orderTotal += prod.Price * prod.TempSqFt;
                 }*/
                OrderHeader orderHeader = new OrderHeader()
                {
                    CreatedByUserId = claim.Value,
                    FinalOrderTotal = ProductUserVM.ProductList.Sum(u => u.Price * u.TempSqFt),
                    City = productUserVm.ApplicationUser.City,
                    StreetAddress = ProductUserVM.ApplicationUser.StreetAddress,
                    State = productUserVm.ApplicationUser.State,
                    PostalCode = productUserVm.ApplicationUser.PostalCode,
                    Email = productUserVm.ApplicationUser.Email,
                    FullName = productUserVm.ApplicationUser.FullName,
                    PhoneNumber = productUserVm.ApplicationUser.PhoneNumber,
                    OrderDate = DateTime.Now,
                    OrderStatus = WC.StatusPending
                };
                _orderHRepo.Add(orderHeader);
                _orderHRepo.Save();

                foreach (var prod in ProductUserVM.ProductList)
                {
                    OrderDetail orderDetail = new OrderDetail()
                    {
                        OrderHeaderId = orderHeader.Id,
                        PricePerSqFt = prod.Price,
                        Sqft = prod.TempSqFt,
                        ProductId = prod.Id
                    };
                    _orderDRepo.Add(orderDetail);
                }
                _orderDRepo.Save();

                string nonceFromTheClient = collection["payment_method_nonce"];
                var request = new TransactionRequest
                {
                    Amount = Convert.ToDecimal(orderHeader.FinalOrderTotal),
                    PaymentMethodNonce = nonceFromTheClient,
                    OrderId = orderHeader.Id.ToString(),
                    Options = new TransactionOptionsRequest
                    {
                        SubmitForSettlement = true
                    }
                };
                var gateway = _brain.GetGateway();
                Result<Transaction> result = gateway.Transaction.Sale(request);
                if (result.Target.ProcessorResponseText == "Approved")
                {
                    orderHeader.TransactionId = result.Target.Id;
                    orderHeader.OrderStatus = WC.StatusApproved;
                }
                else
                {
                    orderHeader.OrderStatus = WC.StatusCancelled;
                }
                _orderHRepo.Save();
                return RedirectToAction(nameof(InquiryConfirmation), new { id = orderHeader.Id} );
            }
            else
            {
                var pathToTemplate = _webHostEnvironment.WebRootPath + Path.DirectorySeparatorChar.ToString()
                + "templates" + Path.DirectorySeparatorChar.ToString() + "Inquiry.html";
                var subject = "New Inquiry";
                string htmlBody = "";
                using (StreamReader sr = System.IO.File.OpenText(pathToTemplate))
                {
                    htmlBody = sr.ReadToEnd();
                }

                StringBuilder productListSB = new StringBuilder();
                foreach (var prod in ProductUserVM.ProductList)
                {
                    productListSB.Append($" - Name: {prod.Name} <span style='font-size:14px;'> (ID: {prod.Id}) </span> <br />");
                }

                string messageBody = string.Format(htmlBody,
                    ProductUserVM.ApplicationUser.FullName,
                    ProductUserVM.ApplicationUser.Email,
                    ProductUserVM.ApplicationUser.PhoneNumber,
                    productListSB.ToString());

                await _emailSender.SendEmailAsync(WC.EmailAdmin, subject, messageBody);

                InquiryHeader inquiryHeader = new InquiryHeader()
                {
                    ApplicationUserId = claim.Value,
                    FullName = ProductUserVM.ApplicationUser.FullName,
                    Email = ProductUserVM.ApplicationUser.Email,
                    PhoneNumber = ProductUserVM.ApplicationUser.PhoneNumber,
                    InquiryDate = DateTime.Now
                };

                _inqHRepo.Add(inquiryHeader);
                _inqHRepo.Save();
                foreach (var prod in ProductUserVM.ProductList)
                {
                    InquiryDetails inquiryDetail = new InquiryDetails()
                    {
                        InquiryHeaderId = inquiryHeader.Id,
                        ProductId = prod.Id
                    };
                    _inqDRepo.Add(inquiryDetail);
                }
                _inqDRepo.Save();

                TempData[WC.Success] = "Inquiry submitted successfully";
            }

            
            return RedirectToAction(nameof(InquiryConfirmation));
        }

        public IActionResult InquiryConfirmation(ProductUserVM productUserVm, int id = 0)
        {
            OrderHeader orderHeader = _orderHRepo.FirstOrDefault(u => u.Id == id);
            HttpContext.Session.Clear();
            return View(orderHeader);
        }

        public IActionResult Remove(int id)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if (HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null
                && HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }
            shoppingCartList.Remove(shoppingCartList.Find(u => u.ProductId == id));
            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);
                
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Clear(int id)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCart(IEnumerable<Product> prodList)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            foreach (var prod in prodList)
            {
                shoppingCartList.Add(new ShoppingCart { ProductId = prod.Id, SqFt = prod.TempSqFt});
            }
            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);
            return RedirectToAction(nameof(Index));
        }
    }
}
