using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
using Newtonsoft.Json.Linq;

namespace Harmony
{
    [Authorize]
    public class UsersController : Controller
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

        /*******************************************
         *          MUSICIAN PROFILE
         *  *************************************/
        // GET: Users/MusicianDetails/5
        public ActionResult MusicianDetails(int? id)
        {
            // No user id passed through
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Viewmodel for Musician
            User user = db.Users.Find(id);

            // If users doesn't exisit
            if (user == null)
            {
                return HttpNotFound();
            }

            var IdentityID = User.Identity.GetUserId();
            MusicianDetailViewModel viewModel = new MusicianDetailViewModel(user);

            viewModel.UpcomingShows = db.User_Show.Where(u => u.MusicianID == user.ID).Select(s => s.Show).Where(s => s.StartDateTime > DateTime.Now && s.Status == "Accepted").OrderByDescending(s => s.EndDateTime).ToList();
            viewModel.VenueList = new SelectList(db.Venues.Where(v => v.User.ASPNetIdentityID == IdentityID), "ID", "VenueName");

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateShow(int? id, MusicianDetailViewModel viewModel)
        {
            // No user id passed through
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = db.Users.Find(id);

            // If users doesn't exisit
            if (user == null)
            {
                return HttpNotFound();
            }

            // Viewmodel for Musician
            MusicianDetailViewModel model = new MusicianDetailViewModel(user);

            var IdentityID = User.Identity.GetUserId();
            model.VenueList = new SelectList(db.Venues.Where(v => v.User.ASPNetIdentityID == IdentityID), "ID", "VenueName");

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
                    Event newEvent = new Event()
                    {
                        Id = Guid.NewGuid().ToString().Replace('-', '0'),
                        Summary = viewModel.Title,
                        Description = viewModel.ShowDescription,
                        Location = db.Venues.Find(viewModel.VenueID).VenueName,
                        Start = new EventDateTime()
                        {
                            DateTime = viewModel.StartDateTime.AddHours(7.0),
                            TimeZone = "America/Los_Angeles"
                        },
                        End = new EventDateTime()
                        {
                            DateTime = viewModel.EndDateTime.AddHours(7.0),
                            TimeZone = "America/Los_Angeles"
                        },
                        Attendees = new List<EventAttendee>()
                        {
                            new EventAttendee() { Email = model.Email }
                        },
                        GuestsCanModify = true
                    };
                    var newEventRequest = service.Events.Insert(newEvent, "primary");
                    // This allows attendees to get email notification
                    newEventRequest.SendNotifications = true;
                    var eventResult = newEventRequest.Execute();

                    // add the new show to db
                    Show newShow = new Show
                    {
                        Title = viewModel.Title,
                        StartDateTime = viewModel.StartDateTime,
                        EndDateTime = viewModel.EndDateTime,
                        Description = viewModel.ShowDescription,
                        DateBooked = newEvent.Created ?? DateTime.Now,
                        VenueID = viewModel.VenueID,
                        Status = "Pending",
                        GoogleEventID = newEvent.Id,
                        ShowOwnerID = db.Users.Where(u => u.ASPNetIdentityID == IdentityID).First().ID 
                    };
                    db.Shows.Add(newShow);
                    User_Show user_Show = new User_Show
                    {
                        MusicianID = model.ID,
                        VenueOwnerID = db.Users.Where(u => u.ASPNetIdentityID == IdentityID).First().ID,
                        ShowID = newShow.ID,
                        MusicianRated = false,
                        VenueRated = false
                    };
                    db.User_Show.Add(user_Show);
                    db.SaveChanges();
                }
                
                return RedirectToAction("MusicianDetails", new { id = model.ID});
            }
            return View(model);
        }

        public ActionResult ViewRatings(int? id)
        {
            User user = db.Users.Find(id);

            IEnumerable<Models.Rating> ratings =
                from r in db.Ratings
                where r.UserID == user.ID
                select r;

            return View(ratings);
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
