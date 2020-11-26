using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rocky_Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rocky_Utility;
using Rocky_DataAccess;
using Rocky_DataAccess.Repository.IRepository;

namespace Rocky.Controllers
{
    [Authorize(Roles = WC.AdminRole)]
    public class ApplicationTypeController : Controller
    {
        private readonly IApplicationTypeRepository _appRepo;
        public ApplicationTypeController(IApplicationTypeRepository appRepo)
        {
            _appRepo = appRepo;
        }
        public IActionResult Index()
        {
            IEnumerable<ApplicationType> objList = _appRepo.GetAll();
            return View(objList);
        }

        //GET - CREATE
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ApplicationType obj)
        {
            if (ModelState.IsValid)
            {
                _appRepo.Add(obj);
                _appRepo.Save();
                TempData[WC.Success] = "Application type created successfully";
                return RedirectToAction("Index");
            }
            TempData[WC.Error] = "Error while creating application type";
            return View(obj);
        }

        //POST - EDIT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(ApplicationType obj)
        {
            if (ModelState.IsValid)
            {
                _appRepo.Update(obj);
                _appRepo.Save();
                TempData[WC.Success] = "Application type edited successfully";
                return RedirectToAction("Index");
            }
            TempData[WC.Error] = "Error while editing application type";
            return View(obj);
        }

        //GET - EDIT
        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
                return NotFound();
            var obj = _appRepo.Find(id.GetValueOrDefault());
            if (obj == null)
                return NotFound();
            return View(obj);
        }

        //POST - DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int? id)
        {
            var obj = _appRepo.Find(id.GetValueOrDefault());
            if (obj == null)
                return NotFound();
            _appRepo.Remove(obj);
            _appRepo.Save();
            TempData[WC.Success] = "Application type deleted successfully";
            return RedirectToAction("Index");
        }

        //GET - DELETE
        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
                return NotFound();
            var obj = _appRepo.Find(id.GetValueOrDefault());
            if (obj == null)
                return NotFound();
            return View(obj);

        }
    }
}
