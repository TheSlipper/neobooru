﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using neobooru.Models;

namespace neobooru.ViewModels
{
    public class ArtThumbnailViewModel
    {
        public readonly string ArtId;
        
        public readonly string FileUrl;

        public readonly string ArtName;

        public readonly string ArtistName;

        public readonly DateTime UploadDate;

        private ArtThumbnailViewModel() {}

        public ArtThumbnailViewModel(Art art)
        {
            ArtId = art.Id.ToString();
            FileUrl = art.PreviewFileUrl;
            ArtName = art.Name;
            ArtistName = art.Author != null ? art.Author.ArtistName : "Unkown";
            UploadDate = art.CreatedAt;
        }
    }
}
