﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using ImageManipulation;
using ImageManipulation.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using neobooru.Models;
using neobooru.ViewModels;
using neobooru.ViewModels.Forms;

namespace neobooru.Controllers
{
    public class ArtistsController : Controller
    {
        private readonly NeobooruDataContext _db;

        private readonly UserManager<NeobooruUser> _userManager;

        private readonly SignInManager<NeobooruUser> _signInManager;

        public ArtistsController(NeobooruDataContext db, UserManager<NeobooruUser> userManager,
            SignInManager<NeobooruUser> signInManager)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private readonly string[] _subsectionPages = {"List", "Register", "Help"};

        #region list

        [HttpGet]
        public async Task<IActionResult> List(int page)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[0];

            List<ArtistThumbnailViewModel> artists = new List<ArtistThumbnailViewModel>();
            await _db.Artists.OrderByDescending(a => a.RegisteredAt).Skip(page * 20).Take(20).ForEachAsync(a =>
            {
                int artCount = _db.Arts.Count(b => b.Author.Id.Equals(a.Id));
                int subCount = _db.ArtistSubscriptions.Count(b => b.Artist.Id.Equals(a.Id));
                artists.Add(new ArtistThumbnailViewModel(a, artCount, subCount));
            });

            ViewBag.PreviousPage = page == 0 ? "" : page.ToString();
            ViewBag.Page = page + 1;
            ViewBag.NextPage = _db.Arts.Count() > (page + 1) * 20 ? (page + 2).ToString() : "";

            return View(artists);
        }

        #endregion

        #region ArtistRegistration

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(ArtistRegistrationViewModel model)
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[1];

            if (!ModelState.IsValid)
                return View();

            if (!_signInManager.IsSignedIn(User))
            {
                ModelState.AddModelError(string.Empty,
                    "You have to be logged in to register an artist!");
                return View();
            }

            Guid id = Guid.NewGuid();

            ImageFileManager ifmPfp = null, ifmBg = null;
            string pfp, bg = null;
            try
            {
                // Load images
                ifmPfp = new ImageFileManager("wwwroot/img/profiles/pfps/", model.Pfp.OpenReadStream(),
                    ImageUtils.ImgExtensionFromContentType(model.Pfp.ContentType));
                if (model.BackgroundImage != null)
                    ifmBg = new ImageFileManager("wwwroot/img/profiles/bgs/",
                        model.BackgroundImage.OpenReadStream(),
                        ImageUtils.ImgExtensionFromContentType(model.BackgroundImage.ContentType));

                // Save images
                pfp = await ifmPfp.SavePfp(id);
                pfp = pfp.Remove(0, 7);
                if (ifmBg != null)
                {
                    bg = await ifmBg.SaveBg(id);
                    bg = bg.Remove(0, 7);
                }
            }
            catch (InvalidArtDimensionsException e)
            {
                ModelState.AddModelError(string.Empty, "Invalid profile picture or background size! " +
                                                       "Profile picture must be at least 400px by 400px and in 1:1 ratio " +
                                                       "and background must be at least 1590px by 540px");
                return View();
            }


            Artist artist = new Artist()
            {
                Id = id,
                ArtistName = model.Name,
                RegisteredAt = DateTime.Now,
                RegisteredBy = await _userManager.GetUserAsync(User),
                ProfileViews = 0,
                BackgroundImageUrl = bg,
                PfpUrl = pfp,
                Country = model.Country,
                FacebookProfileUrl = model.FacebookProfileUrl,
                TwitterProfileUrl = model.TwitterProfileUrl,
                MailAddress = model.MailAddress,
                Gender = model.Gender,
            };

            await _db.Artists.AddAsync(artist);
            await _db.SaveChangesAsync();

            return Redirect("/Artists/List");
        }

        #endregion

        [HttpGet]
        public async Task<IActionResult> Artist(string artistId)
        {
            List<ArtThumbnailViewModel> list = new List<ArtThumbnailViewModel>();

            Artist artist = await _db.Artists.FirstOrDefaultAsync(a => a.Id.ToString().Equals(artistId));
            if (artist == null)
                return Redirect("/Artists/List");

            var exp = new Action<Art>(a => list.Add(new ArtThumbnailViewModel(a)));
            var arts = _db.Arts
                .Where(a => a.Author.Id.ToString().Equals(artistId)).OrderByDescending(a => a.Stars);
            if (arts.Count() > 5)
                await arts.Take(5).ForEachAsync(exp);
            else
                await arts.ForEachAsync(exp);

            IQueryable<ArtistSubscription> subscriptions = _db.ArtistSubscriptions
                .Where(a => a.Artist.Id.Equals(artist.Id));
            int likes = 0;
            await _db.Arts.Include(a => a.Author).Include(a => a.Likes)
                .ForEachAsync(a =>
                {
                    if (a.Author.Id.ToString().Equals(artistId))
                        likes += a.Likes.Count();
                });

            ArtistViewModel avm = new ArtistViewModel(artist, list, subscriptions.Count(), likes);


            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = "Artist";

            return View(avm);
        }

        [HttpGet]
        public IActionResult Help()
        {
            ViewBag.SubsectionPages = _subsectionPages;
            ViewBag.ActiveSubpage = _subsectionPages[2];
            return View();
        }
    }
}