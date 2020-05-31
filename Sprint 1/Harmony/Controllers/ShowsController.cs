﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Harmony.DAL;
using Harmony.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Calendar.ASP.NET.MVC5;
using System.IO;
using Google.GData.Extensions;
using Calendar.ASP.NET.MVC5.Models;
using System.Threading.Tasks;

namespace Harmony.Controllers
{
    [Authorize]
    public class ShowsController : Controller
    {
        private HarmonyContext db = new HarmonyContext();
        private readonly IDataStore dataStore = new FileDataStore(GoogleWebAuthorizationBroker.Folder);

        // Get user's Google Calendar info
        private async Task<UserCredential> GetCredentialForApiAsync()
        {
            var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {

                    ClientId = MyClientSecrets.ClientId,
                    ClientSecret = MyClientSecrets.ClientSecret,
                },
                Scopes = MyRequestedScopes.Scopes,
            };
            var flow = new GoogleAuthorizationCodeFlow(initializer);

            var identity = await HttpContext.GetOwinContext().Authentication.GetExternalIdentityAsync(
                DefaultAuthenticationTypes.ApplicationCookie);
            var userId = identity.FindFirstValue(MyClaimTypes.GoogleUserId);

            var token = await dataStore.GetAsync<TokenResponse>(userId);
            return new UserCredential(flow, userId, token);
        }

        // GET: Shows
        public ActionResult MyShows()
        {
            var identityID = User.Identity.GetUserId();
            User user = db.Users.Where(u => u.ASPNetIdentityID == identityID).FirstOrDefault();
            List<Show> FinishedShows = db.Shows.Where(s => (s.EndDateTime < DateTime.Now) && (s.Status == "Accepted" || s.Status == "Pending")).ToList();
            foreach(var finishedshow in FinishedShows)
            {
                finishedshow.Status = "Finished";
            }
            db.SaveChanges();
            if (User.IsInRole("Musician"))
            {
                User musician = db.Users.Where(u => u.ASPNetIdentityID == identityID).FirstOrDefault();
                return View(db.User_Show.Where(u => u.MusicianID == musician.ID).Select(s => s.Show).OrderByDescending(s => s.EndDateTime).ToList());
            }
            if (User.IsInRole("VenueOwner"))
            {
                User venueOwner = db.Users.Where(u => u.ASPNetIdentityID == identityID).FirstOrDefault();
                return View(db.User_Show.Where(u => u.VenueOwnerID == venueOwner.ID).Select(s => s.Show).OrderByDescending(s => s.EndDateTime).ToList());
            }
            
            return View(db.Shows.ToList());
        }

        // GET: Shows/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Show show = db.Shows.Find(id);
            if (show == null)
            {
                return HttpNotFound();
            }
            User_Show user_Show = db.User_Show.Where(u => u.ShowID == id).First();
            ShowsViewModel viewModel = new ShowsViewModel(user_Show);
            viewModel.VenueList = new SelectList(db.Venues.Where(s => s.UserID == show.Venue.UserID), "ID", "VenueName");
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int? id, ShowsViewModel viewModel)
        {
            // No user id passed through
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Show show = db.Shows.Find(id);

            // If users doesn't exisit
            if (show == null)
            {
                return HttpNotFound();
            }

            // Viewmodel for Show
            ShowsViewModel model = new ShowsViewModel(show);

            var IdentityID = User.Identity.GetUserId();
            model.VenueList = new SelectList(db.Venues.Where(s => s.UserID == show.Venue.UserID), "ID", "VenueName");

            if (ModelState.IsValid)
            {
                // Get user's calendar credentials
                UserCredential credential = await GetCredentialForApiAsync();
                // Create Google Calendar API service.
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Harmony",
                });

                // Fetch the list of calendars.
                var calendars = await service.CalendarList.List().ExecuteAsync();
                // create a new event to google calendar
                if (calendars != null)
                {
                    Event updatedEvent = new Event()
                    {
                        Summary = viewModel.Title,
                        Description = viewModel.Description,
                        Location = db.Venues.Find(viewModel.VenueID).VenueName,
                        Start = new EventDateTime()
                        {
                            DateTime = viewModel.StartTime.AddHours(7.0),
                            TimeZone = "America/Los_Angeles"
                        },
                        End = new EventDateTime()
                        {
                            DateTime = viewModel.EndTime.AddHours(7.0),
                            TimeZone = "America/Los_Angeles"
                        },
                        Attendees = new List<EventAttendee>()
                        {
                            new EventAttendee() { Email = show.Venue.User.Email },
                            new EventAttendee() { Email = db.Users.Where(u => u.ID == model.MusicianID).FirstOrDefault().Email }
                        }
                    };
                    var newEventRequest = service.Events.Update(updatedEvent, "primary", show.GoogleEventID);
                    // This allows attendees to get email notification
                    newEventRequest.SendNotifications = true;
                    var eventResult = newEventRequest.Execute();

                    // add the new show to db
                    show.Title = viewModel.Title;
                    show.StartDateTime = viewModel.StartTime;
                    show.EndDateTime = viewModel.EndTime;
                    show.Description = viewModel.Description;
                    show.VenueID = viewModel.VenueID;
                    db.SaveChanges();

                    return RedirectToAction("Details", new { id = model.ShowID });
                }
            }
            return View(model);
        }

        [HttpPost]
        public ActionResult Accept(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Show show = db.Shows.Find(id);
            if (show == null)
            {
                return HttpNotFound();
            }

            if (ModelState.IsValid)
            {
                show.Status = "Accepted";
                db.SaveChanges();
                return RedirectToAction("MyShows");

            }
            return RedirectToAction("MyShows");
        }

        [HttpPost]
        public async Task<ActionResult> Decline(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Show show = db.Shows.Find(id);
            if (show == null)
            {
                return HttpNotFound();
            }

            var IdentityID = User.Identity.GetUserId();

            if (ModelState.IsValid)
            {
                // Get user's calendar credentials
                UserCredential credential = await GetCredentialForApiAsync();
                // Create Google Calendar API service.
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Harmony",
                });

                // Fetch the list of calendars.
                var calendars = await service.CalendarList.List().ExecuteAsync();
                // update an event to google calendar
                if (calendars != null)
                {
                    show.Status = "Declined";
                    db.SaveChanges();

                    var DeleteRequest = service.Events.Delete("primary", show.GoogleEventID);
                    // This allows attendees to get email notification
                    DeleteRequest.SendNotifications = true;
                    DeleteRequest.SendUpdates = 0;
                    var eventResult = DeleteRequest.ExecuteAsync();

                    
                }
                return RedirectToAction("MyShows");
                
            }
            return RedirectToAction("MyShows");
        }

        [HttpPost]
        public async Task<ActionResult> Cancel(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Show show = db.Shows.Find(id);
            if (show == null)
            {
                return HttpNotFound();
            }

            var IdentityID = User.Identity.GetUserId();

            if (ModelState.IsValid)
            {
                // Get user's calendar credentials
                UserCredential credential = await GetCredentialForApiAsync();
                // Create Google Calendar API service.
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Harmony",
                });

                // Fetch the list of calendars.
                var calendars = await service.CalendarList.List().ExecuteAsync();
                // update an event on google calendar
                if (calendars != null)
                {
                    show.Status = "Canceled";
                    db.SaveChanges();

                    var DeleteRequest = service.Events.Delete("primary", show.GoogleEventID);
                    // This allows attendees to get email notification
                    DeleteRequest.SendNotifications = true;
                    DeleteRequest.SendUpdates = 0;
                    var eventResult = DeleteRequest.ExecuteAsync();
                }
                return RedirectToAction("MyShows");

            }
            return RedirectToAction("MyShows");
        }

        /*********************************
         *          RATE SHOWS
         * ******************************/
        public int getRating(string ratingStr)
        {
            if (ratingStr == "star1")
            {
                return 1;
            }
            else if (ratingStr == "star2")
            {
                return 2;
            }
            else if (ratingStr == "star3")
            {
                return 3;
            }
            else if (ratingStr == "star4")
            {
                return 4;
            }
            else
            {
                return 5;
            }
        }
        public ActionResult RateUser(int? id)
        {
            User_Show show = db.User_Show.Where(s => s.ShowID == id).FirstOrDefault();
            ShowsViewModel viewModel = new ShowsViewModel(show);
            return View(viewModel);
        }

        public double CalcAveRating(int userId, int numStars)
        {
            User user = db.Users.Where(u => u.ID == userId).FirstOrDefault();

            var ratings = from r in db.Ratings
                          where r.UserID == user.ID
                          select r;

            double aveRating = 0;
            double numRatings = 1;
            double totalRating = numStars;

            foreach (var r in ratings)
            {
                numRatings++;
                totalRating += r.Value;
            }

            aveRating = totalRating / numRatings;

            aveRating = Math.Round(aveRating, 2);

            return aveRating;
        }

        [HttpPost]
        public ActionResult RateUser(int? id, ShowsViewModel model)
        {
            User_Show show = db.User_Show.Where(s => s.ShowID == id).FirstOrDefault();
            ShowsViewModel viewModel = new ShowsViewModel(show);
            // Converting string into int
            int numStars = getRating(model.RatingValue);

            Models.Rating userRating = new Models.Rating();

            if (User.IsInRole("VenueOwner"))
            {
                User user = db.Users.Where(u => u.ID == viewModel.MusicianID).FirstOrDefault();
                userRating.UserID = viewModel.MusicianID;
                userRating.Value = numStars;
                show.VenueRated = true;
                user.AveRating = CalcAveRating(user.ID, numStars);
            }
            else if (User.IsInRole("Musician"))
            {
                User user = db.Users.Where(u => u.ID == viewModel.VenueID).FirstOrDefault();
                userRating.UserID = viewModel.VenueID;
                userRating.Value = numStars;
                show.MusicianRated = true;
                user.AveRating = CalcAveRating(user.ID, numStars);
            }

            db.Ratings.Add(userRating);
            db.SaveChanges();

            return RedirectToAction("MyShows");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
