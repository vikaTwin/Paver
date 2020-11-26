using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rocky_Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Rocky_Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Rocky_Utility;
using Rocky_DataAccess;
using Rocky_DataAccess.Repository.IRepository;

namespace Rocky.Controllers
{
    [Authorize(Roles = WC.AdminRole)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _prodRepo;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IProductRepository prodRepo, IWebHostEnvironment webHostEnvironment)
        {
            _prodRepo = prodRepo;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> objList = _prodRepo.GetAll(includeProperties: "Category, ApplicationType");
            //the same
            /*foreach (var obj in objList)
            {
                obj.Category = _db.Category.FirstOrDefault(u => u.Id == obj.CategoryId);
                obj.ApplicationType = _db.ApplicationType.FirstOrDefault(u => u.Id == obj.ApplicationTypeId);
            }*/
            return View(objList);
        }
        //GET - UPSERT
        public IActionResult Upsert(int? id)
        {
            ProductVM pVM = new ProductVM()
            {
                Product = new Product(),
                CategorySelectList = _prodRepo.GetAllDropDownList(WC.CategoryName),
                ApplicationTypeSelectList = _prodRepo.GetAllDropDownList(WC.CategoryName)
            };
            
            if (id == null)
            {
                //this is creation
                return View(pVM);
            }
            else
            {
                pVM.Product = _prodRepo.Find(id.GetValueOrDefault());
                if (pVM.Product == null)
                    return NotFound();
                return View(pVM);
            }
        }

        //POST - UPSERT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(ProductVM obj)
        {
            if (ModelState.IsValid)
            {
                var files = HttpContext.Request.Form.Files;
                string webRootPath = _webHostEnvironment.WebRootPath;
                if (obj.Product.Id == 0)
                {
                    //create
                    string upload = webRootPath + WC.ImagePath;
                    string fileName = Guid.NewGuid().ToString();
                    string extension = Path.GetExtension(files[0].FileName);
                    using (var fileStream = new FileStream(Path.Combine(upload, fileName+extension), FileMode.Create))
                    {
                        files[0].CopyTo(fileStream);
                    }
                    obj.Product.Image = fileName + extension;
                    _prodRepo.Add(obj.Product);
                }
                else 
                {
                    //update
                    var objFromDb = _prodRepo.FirstOrDefault(u => u.Id == obj.Product.Id, isTracking: false);
                    
                    if (files.Count() > 0)
                    {
                        string upload = webRootPath + WC.ImagePath;
                        string fileName = Guid.NewGuid().ToString();
                        string extension = Path.GetExtension(files[0].FileName);

                        var oldFile = obj.Product.Image;
                        if (System.IO.File.Exists(oldFile))
                        {
                            System.IO.File.Delete(oldFile);
                        }

                        using (var fileStream = new FileStream(Path.Combine(upload, fileName + extension), FileMode.Create))
                        {
                            files[0].CopyTo(fileStream);
                        }

                        obj.Product.Image = fileName + extension;
                    }
                    else
                    {
                        obj.Product.Image = objFromDb.Image;

                    }
                    _prodRepo.Update(obj.Product);
                }
                _prodRepo.Save();
                TempData[WC.Success] = "Action completed successfully";
                return RedirectToAction("Index");
            }

            obj.CategorySelectList = _prodRepo.GetAllDropDownList(WC.CategoryName);
            obj.ApplicationTypeSelectList = _prodRepo.GetAllDropDownList(WC.CategoryName);

            return View(obj);
        }

        //GET - DELETE
        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
                return NotFound();

            Product product = _prodRepo.FirstOrDefault(u => u.Id == id, includeProperties: "Category, ApplicationType");

            if (product == null)
                return NotFound();
            return View(product);
        }

        //POST - DELETE
        [HttpPost]
        [ValidateAntiForgeryToken, ActionName("Delete")]
        public IActionResult DeletePost(int? id)
        {
            var obj = _prodRepo.Find(id.GetValueOrDefault());
            if (obj == null)
                return NotFound();

            string upload = _webHostEnvironment.WebRootPath + WC.ImagePath;
            var oldFile = Path.Combine(upload, obj.Image);

            if (System.IO.File.Exists(oldFile))
            {
                System.IO.File.Delete(oldFile);
            }

            _prodRepo.Remove(obj);
            _prodRepo.Save();
            TempData[WC.Success] = "Product deleted successfully";
            return RedirectToAction("Index");
        }

    }
}
