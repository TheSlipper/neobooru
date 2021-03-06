﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Differencing;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using neobooru.Models;
using neobooru.ViewModels;
using neobooru.ViewModels.Forms;

namespace neobooru.Controllers
{
    public class AdministrationPanelController : Controller
    {
        
        private readonly string[] _subsectionPages = { "Main Panel", "List Users", "List Roles", "Create Role", "Help" };

        private readonly UserManager<NeobooruUser> _userManager;
        
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdministrationPanelController(UserManager<NeobooruUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<bool> UserIsAdmin()
        {
            NeobooruUser usr = await _userManager.GetUserAsync(User);
            return await _userManager.IsInRoleAsync(usr, "root");
        }

        [HttpGet]
        public IActionResult MainPanel()
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[0];
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ListUsers()
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");
            
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];

            IQueryable<NeobooruUser> users = _userManager.Users;
            List<UserListingView> dto = new List<UserListingView>();

            foreach (var usr in users)
            {
                var roles = await _userManager.GetRolesAsync(usr);
                StringBuilder sb = new StringBuilder();
                if (roles.Count > 0)
                {
                    sb.Append(roles[0]);
                    for (int i = 1; i < roles.Count; i++)
                    {
                        sb.Append(", ");
                        sb.Append(roles[i]);
                    }
                }
                dto.Add(new UserListingView(usr.Id, usr.UserName, usr.Email, usr.RegisteredOn, sb.ToString()));
            }

            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> EditUsersRoles(string id)
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "Edit User";

            NeobooruUser usr = await _userManager.FindByIdAsync(id);
            var usrRoles = await _userManager.GetRolesAsync(usr);
            string[] arr = new string[usrRoles.Count];
            usrRoles.CopyTo(arr, 0);
            IQueryable<IdentityRole> roles = _roleManager.Roles;
            RoleCheckboxesViewModel rcvm = new RoleCheckboxesViewModel();
            rcvm.UserId = usr.Id;
            foreach (var role in roles)
            {
                RoleCheckboxViewModel vm = new RoleCheckboxViewModel();
                vm.RoleName = role.Name;
                vm.RoleId = role.Id;
                if (arr.Contains(role.Name))
                    vm.IsChecked = true;
                else
                    vm.IsChecked = false;
                rcvm.Checkboxes.Add(vm);
            }

            return View(rcvm);
        }

        [HttpPost]
        public async Task<IActionResult> EditUsersRoles(RoleCheckboxesViewModel viewModel)
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            NeobooruUser usr = await _userManager.FindByIdAsync(viewModel.UserId);
            foreach (var roleCheckboxViewModel in viewModel.Checkboxes)
            {
                if (roleCheckboxViewModel.IsChecked)
                    await _userManager.AddToRoleAsync(usr, roleCheckboxViewModel.RoleName);
                else
                    await _userManager.RemoveFromRoleAsync(usr, roleCheckboxViewModel.RoleName);
            }
            return RedirectToAction("ListUsers", "AdministrationPanel");
        }

        [HttpGet]
        public async Task<IActionResult> ListRoles()
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[2];
            var roles = _roleManager.Roles;

            return View(roles);
        }

        [HttpGet]
        public async Task<IActionResult> CreateRole()
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[3];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(CreateRoleViewModel createRoleViewModel)
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            
            if (ModelState.IsValid)
            {
                IdentityRole identityRole = new IdentityRole 
                {
                    Name = createRoleViewModel.RoleName
                };

                IdentityResult result = await _roleManager.CreateAsync(identityRole);
                if (result.Succeeded)
                    return RedirectToAction("ListRoles", "AdministrationPanel");
                foreach (IdentityError error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[3];
            return View(createRoleViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> EditRole(string id)
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "Edit Role";

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                ViewBag.ErrorMessage = $"Role with Id = {id} can not be found!";
                return View("NotFound");
            }

            EditRoleViewModel ervmModel = new EditRoleViewModel
            {
                Id = role.Id,
                RoleName = role.Name
            };

            // https://youtu.be/7ikyZk5fGzk
            // foreach(var user in userManager.Users)
            // {
            //      if (await userManager.IsInRoleAsync(user, role.Name))
            //      {
            //          model.Users.Add(user.UserName);
            //      }

            return View(ervmModel);
        }

        [HttpPost]
        public async Task<IActionResult> EditRole(EditRoleViewModel ervmModel)
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");
            
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "Edit Role";

            var role = await _roleManager.FindByIdAsync(ervmModel.Id);
            if (role == null)
            {
                ViewBag.ErrorMessage = $"Role with Id = {ervmModel.Id} can not be found!";
                return View("NotFound");
            }
            else
            {
                role.Name = ervmModel.RoleName;
                var result = await _roleManager.UpdateAsync(role);
                if (result.Succeeded)
                    return RedirectToAction("ListRoles");
                foreach(var err in result.Errors)
                    ModelState.AddModelError("", err.Description);
            }

            return View(ervmModel);
        }


        [HttpGet]
        public async Task<IActionResult> Help()
        {
            if (!await UserIsAdmin())
                return Redirect("/Profile/Profile");
            
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[4];
            return View();
        }
    }
}