﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ImageManipulation;
using ImageManipulation.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using neobooru.Models;
using neobooru.Services;
using neobooru.ViewModels;
using neobooru.ViewModels.Forms;

namespace neobooru.Controllers
{
    public class ProfileController : Controller
    {
        private UserManager<NeobooruUser> _userManager;

        private SignInManager<NeobooruUser> _signInManager;

        private IMailService _mailService;

        private readonly NeobooruDataContext _db;

        private readonly string[] _subsectionPages = {"Profile", "Settings", "Help"};

        public ProfileController(NeobooruDataContext db, UserManager<NeobooruUser> userManager,
            SignInManager<NeobooruUser> signInManager, IMailService mailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _mailService = mailService;
        }

        [HttpGet]
        public async Task<IActionResult> Profile(string profileId)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[0];

            if (profileId == null && _signInManager.IsSignedIn(User))
                profileId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            else
                Redirect("/");

            NeobooruUser user = _db.NeobooruUsers
                .Include(usr => usr.ArtLikes)
                .Include(usr => usr.UploadedArts)
                .Include(usr => usr.Subscriptions)
                .ThenInclude(s => s.Artist)
                .FirstOrDefault(a => a.Id.Equals(profileId));

            if (user == null)
                Redirect("/");

            List<ArtThumbnailViewModel> recentlyUploaded = user.UploadedArts.OrderByDescending(usr => usr.CreatedAt)
                .Take(5).Select(a => new ArtThumbnailViewModel(a)).ToList();
            var recentlyLiked = user.ArtLikes.OrderByDescending(al => al.LikedDate).Take(5)
                .Select(a => new ArtThumbnailViewModel(a.LikedArt)).ToList();
            var recentlySubbed = user.Subscriptions
                .OrderByDescending(sub => sub.SubscribedOn)
                .Take(5).Select(s =>
                {
                    var foo = _db.ArtLikes.Include(f => f.LikedArt)
                        .ThenInclude(f => f.Author)
                        .Count(f => f.LikedArt.Author.Id.ToString()
                            .Equals(s.Artist.Id.ToString()));
                    return new ArtistThumbnailViewModel(s.Artist, s.Artist.Arts.Count(),
                        s.Artist.Subscriptions.Count(), foo);
                }).ToList();

            return View(new ProfileViewModel(user, recentlyUploaded, recentlyLiked, recentlySubbed, profileId));
        }

        [HttpGet]
        public IActionResult Likes(string profileId)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "User's Likes";
            
            var usr = _db.NeobooruUsers
                .Include(usr => usr.ArtLikes)
                .ThenInclude(likes => likes.LikedArt)
                .FirstOrDefault(a => a.Id.Equals(profileId));

            if (usr == null)
                Redirect("/");

            List<ArtThumbnailViewModel> likes = usr.ArtLikes.OrderByDescending(al => al.LikedDate)
                .Select(a => new ArtThumbnailViewModel(a.LikedArt)).ToList();
            AllArtThumbnailsViewModel lvm = new AllArtThumbnailsViewModel()
            {
                Username = usr.UserName,
                ProfileId = profileId,
                PfpUrl = usr.PfpUrl,
                BackgroundUrl = usr.BgUrl,
                Description = usr.ProfileDescription,
                Thumbnails = likes
            };

            return View(lvm);
        }

        [HttpGet]
        public IActionResult Subscriptions(string profileId)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "User's Subscriptions";
            
            var usr = _db.NeobooruUsers
                .Include(usr => usr.Subscriptions)
                .ThenInclude(subs => subs.Artist)
                .FirstOrDefault(a => a.Id.Equals(profileId));

            if (usr == null)
                Redirect("/");

            Func<ArtistSubscription, ArtistThumbnailViewModel> subToThumbnail = (s) =>
            {
                int likes = _db.ArtLikes.Include(s => s.LikedArt.Author)
                    .Count(l => l.LikedArt.Author.Id.ToString().Equals(s.Artist.Id.ToString()));
                return new ArtistThumbnailViewModel(s.Artist, s.Artist.Arts.Count(),
                    s.Artist.Subscriptions.Count(), likes);
            };
            List<ArtistThumbnailViewModel> thumbnails = _db.ArtistSubscriptions
                .Include(s => s.Artist)
                .ThenInclude(s => s.Arts)
                .Include(s => s.Artist.Subscriptions)
                .Where(s => s.Subscriber.Id.Equals(profileId))
                .OrderByDescending(s => s.SubscribedOn)
                .Select(subToThumbnail).ToList();
            
            SubscriptionsViewModel svm = new SubscriptionsViewModel()
            {
                Username = usr.UserName,
                ProfileId = profileId,
                PfpUrl = usr.PfpUrl,
                BackgroundUrl = usr.BgUrl,
                Description = usr.ProfileDescription,
                Thumbnails = thumbnails
            };
            
            return View(svm);
        }

        [HttpGet]
        public IActionResult Uploads(string profileId)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "User's Subscriptions";
            
            var usr = _db.NeobooruUsers
                .Include(usr => usr.Subscriptions)
                .ThenInclude(subs => subs.Artist)
                .FirstOrDefault(a => a.Id.Equals(profileId));

            if (usr == null)
                Redirect("/");

            List<ArtThumbnailViewModel> uploads = _db.Arts.Where(a => a.Uploader.Id.Equals(profileId))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ArtThumbnailViewModel(a)).ToList();
            
            AllArtThumbnailsViewModel vm = new AllArtThumbnailsViewModel()
            {
                Username = usr.UserName,
                ProfileId = profileId,
                PfpUrl = usr.PfpUrl,
                BackgroundUrl = usr.BgUrl,
                Description = usr.ProfileDescription,
                Thumbnails = uploads
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult Registration()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Profile", "Profile");
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Registration(BasicUserRegistrationViewModel model)
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Profile", "Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];
            if (ModelState.IsValid)
            {
                NeobooruUser user = new NeobooruUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    RegisteredOn = DateTime.Now
                };
                IdentityResult result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Generate email confirmation link
                    string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Profile", new
                    {
                        userId = user.Id,
                        token = token,
                    }, Request.Scheme);
                    
                    // Send the mail
                    MailRequest mr = new MailRequest()
                    {
                        ToEmail = user.Email,
                        Subject = "Confirm your account's email",
                        Body = "Please click this link to confirm your account: " + confirmationLink
                    };
                    await _mailService.SendEmailAsync(mr);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("index", "Home");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return RedirectToAction("index", "home");
            NeobooruUser user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return RedirectToAction("index", "home");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
                return RedirectToAction("Profile", "Profile");
            return RedirectToAction("index", "home");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Profile", "Profile");
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "Login";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Profile", "Profile");

            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    // TODO: This doesn't redirect to the returnUrl despite populating the string variable
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction("index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid Login Attempt");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Settings()
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];
            
            if (!_signInManager.IsSignedIn(User))
            {
                ModelState.AddModelError(string.Empty,
                    "You have to be logged in to change your settings!");
                return Redirect("/Profile/Settings");
            }

            NeobooruUser usr = await _userManager.GetUserAsync(User);
            ProfileUpdateViewModel puvm = new ProfileUpdateViewModel()
            {
                Gender = usr.Gender,
                Username = usr.UserName,
                ProfileDescription = usr.ProfileDescription,
            };


            return View(puvm);
        }

        public IActionResult Help()
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[2];
            return View();
        }

        public async Task<IActionResult> Update(ProfileUpdateViewModel puvm)
        {
            if (!ModelState.IsValid)
                return Redirect("/Profile/Settings");
            
            if (!_signInManager.IsSignedIn(User))
            {
                ModelState.AddModelError(string.Empty,
                    "You have to be logged in to change your settings!");
                return Redirect("/Profile/Settings");
            }

            NeobooruUser usr = await _userManager.GetUserAsync(User);
            usr.Gender = puvm.Gender;
            usr.UserName = puvm.Username;
            usr.ProfileDescription = puvm.ProfileDescription;

            if (puvm.PfpImage != null)
            {
                Guid id = Guid.NewGuid();

                ImageFileManager ifmPfp = null;
                try
                {
                    ifmPfp = new ImageFileManager("wwwroot/img/profiles/pfps/",
                        puvm.PfpImage.OpenReadStream(), ImageUtils.ImgExtensionFromContentType(puvm.PfpImage.ContentType));
                }
                catch (InvalidArtDimensionsException e)
                {
                    ModelState.AddModelError(string.Empty, "Invalid profile picture or background size! " +
                                                           "Profile picture must be at least 400px by 400px");
                    return Redirect("/Profile/Settings");
                }

                usr.PfpUrl = await ifmPfp.SavePfp(id);
                usr.PfpUrl = usr.PfpUrl.Remove(0, 7);
            }

            if (puvm.BgImage != null)
            {
                Guid id = Guid.NewGuid();

                ImageFileManager ifmPfp = null;
                try
                {
                    ifmPfp = new ImageFileManager("wwwroot/img/profiles/bgs/",
                        puvm.BgImage.OpenReadStream(), ImageUtils.ImgExtensionFromContentType(puvm.BgImage.ContentType));
                }
                catch (InvalidArtDimensionsException e)
                {
                    ModelState.AddModelError(string.Empty, "Invalid profile picture or background size! " +
                                                           "Background must be at least 1590px by 540px");
                    return Redirect("/Profile/Settings");
                }

                usr.BgUrl = await ifmPfp.SaveBg(id);
                usr.BgUrl = usr.BgUrl.Remove(0, 7);
            }

            await _db.SaveChangesAsync();
            return Redirect("/Profile/Profile");
        }
    }
}