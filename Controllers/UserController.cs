﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.MSIdentity.Shared;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using TeamRedInternalProject.Models;
using TeamRedInternalProject.Repositories;
using TeamRedInternalProject.ViewModel;

namespace TeamRedInternalProject.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly UserRepo _userRepo;
        private readonly TicketRepo _ticketRepo;
        private readonly TicketTypeRepo _ticketTypeRepo;
        private readonly ConcertContext _db;
        
        public UserController(ILogger<UserController> logger)
        {
            _db = new();
            _logger = logger;
            _userRepo = new UserRepo();
            _ticketRepo= new TicketRepo();
            _ticketTypeRepo = new TicketTypeRepo();
        }

        // Display all tickets according to user
        public IActionResult Index()
        {
            string email = User.Identity.Name;
            User user = _userRepo.GetUsersByEmail(email);
            List<Ticket> ticketList = _ticketRepo.GetUserTickets(email);


            return View(ticketList);
        }

        //Buy Tickets
        public IActionResult PurchaseTickets()
        {
            //string email = User.Identity.Name;
            //User user = _userRepo.GetUsersByEmail(email);

            List<TicketType> ticketTypes = _ticketTypeRepo.GetTicketTypes();

            List<Ticket> tickets = _ticketRepo.GetAllTickets();

            List<TicketOptionVM> ticketOptions = new List<TicketOptionVM>();

            foreach (TicketType ticketType in ticketTypes)
            {
                int qtyTicketsSoldOfType = tickets.Where(t => t.Festival.IsCurrent).Where(t => t.TicketTypeId == ticketType.TicketTypeId).Count();
                int? qtyTicketsAvailableOfType = _db.FestivalTicketTypes.Where(ftt => ftt.TicketTypeId == ticketType.TicketTypeId && ftt.Festival.IsCurrent).Select(ftt => ftt.Quantity).FirstOrDefault();
                int qtyTicketsRemainingOfType = (int)((qtyTicketsAvailableOfType != null) ? qtyTicketsAvailableOfType - qtyTicketsSoldOfType : 0);

                TicketOptionVM ticketOption = new TicketOptionVM()
                {
                    TicketType = ticketType,
                    QtyRemaining = qtyTicketsRemainingOfType
                };
                ticketOptions.Add(ticketOption);
            }

            return View(ticketOptions);

        }

        //HTTP Post Create
        [HttpPost]

        public string PaySuccess([FromBody] PurchaseDetailsVM purchaseDetails)
        {
            // CA MAR 3/23: this is no longer PayerEmail. Email returned from PayPal was null. Now assigning to User Email.
            //Order Email
            purchaseDetails.PayerEmail = User.Identity.Name;
            User user = _userRepo.GetUsersByEmail(purchaseDetails.PayerEmail);

            Festival currentFestival = _db.Festivals.Where(a => a.IsCurrent == true).FirstOrDefault();
            int currentFestivalId = currentFestival.FestivalId;
            
            DateTime orderDate = DateTime.Now;
            Order order = new Order()
            {
                OrderDate = orderDate,
                Email = purchaseDetails.PayerEmail
            };

            _db.Orders.Add(order);
            _db.SaveChanges();

            foreach (TicketRequestVM ticketRequest in purchaseDetails.TicketRequests)
            {
                for (int i = 0; i < ticketRequest.quantity;)
                {
                    
                    TicketType ticketType = _db.TicketTypes.Where(tt => tt.Type == ticketRequest.ticketType).FirstOrDefault();
                    int ticketTypeId = ticketType.TicketTypeId;
                    

                    Ticket ticket = new Ticket()
                    {
                        OrderId = order.OrderId,
                        FestivalId = currentFestivalId,
                        TicketTypeId = ticketTypeId,
                    };

                    _db.Tickets.Add(ticket);
                    _db.SaveChanges();

                    i++;
                }
            }

            return JsonConvert.SerializeObject(order.OrderId);
        }

        public IActionResult PurchaseConfirmation(int id)
        {
            Order order = _db.Orders.Where(o => o.OrderId == id).FirstOrDefault();
            List<Ticket> tickets  = _db.Tickets.Where(t => t.OrderId == id).ToList();
            Dictionary<string, int> ticketTypeDict = new Dictionary<string, int>();

            HashSet<string> ticketTypeSet = new HashSet<string>();
            List<string> ticketTypes = new List<string>();
            string ticketTypeName = "";

            //Console.WriteLine(string.Join(", ", keySet));
            //Console.ReadLine();

            //Get Ticket Types Names
            //Display only 1 of each Ticket Name
            //Get Quantities
            //Get Order Date

            string orderDate = order.OrderDate.ToString("MMM dd, yyyy");

            foreach(Ticket ticket in tickets)
            {
                TicketType ticketType = _db.TicketTypes.Where(tt => tt.TicketTypeId == ticket.TicketTypeId).FirstOrDefault();
                ticketTypeName = ticketType.Type;
                ticketTypes.Add(ticketTypeName);
            }

            ticketTypeSet.Add(ticketTypeName);
            foreach (string type in ticketTypes)
            {
                try
                {
                    ticketTypeDict.Add(ticketTypeName, 1);
                }
                catch
                {
                    //update count in dict:
                    if(ticketTypeName == type)
                    {
                        int i = 0;
                        foreach (string ttn in ticketTypes)
                        {
                            i++;
                        }
                        
                        ticketTypeDict[ticketTypeName] = i;
                    }
                    
                }


            }

            foreach (var entry in ticketTypeDict)
            {
                Console.WriteLine($"{entry.Key} : {entry.Value}");
            }

            return View(id);
        }

        //public void PaySuccess([FromBody] PurchaseDetailsVM purchaseDetails)
        //public void PaySuccess([FromBody] PurchaseDetailsVM purchaseDetails)
        ////public JsonResponse PaySuccess([FromBody] string purchaseDetails)
        //{
        //    string email = User.Identity.Name;
        //    User user = _userRepo.GetUsersByEmail(email);

        //    Console.WriteLine("purchasedetails", purchaseDetails);
        //    string updatedPurchaseDetails = purchaseDetails.ToString();
        //    Console.WriteLine("udpated purchase details", updatedPurchaseDetails);


        //}

        ////Ticket Confirm Details
        //public IActionResult Details()
        //{
        //    return View();
        //}

        ////Post Details, Payment 



        // edit user profile get method
        // get user email from session,
        public IActionResult EditProfile()
        {
            string email = User.Identity.Name;
            User user = _userRepo.GetUsersByEmail(email);

            return View(user);
        }

        [HttpPost]
        // Post edit user profile
        public IActionResult EditProfile(User user)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var updatedUser = _userRepo.EditUser(user);
                    //Change to redirect to MyTickets
                    //We need to get tickets for user and pass to view
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ViewBag.Message = ex.Message;
                    return View(user);
                }
            }
            ViewBag.Message = "input fields invalid";
            return View(user);
        }
    }
}
