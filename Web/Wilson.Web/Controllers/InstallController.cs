﻿using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wilson.Companies.Core.Entities;
using Wilson.Companies.Data.DataAccess;
using Wilson.Web.Models.InstallViewModels;
using Wilson.Web.Seed;

namespace Wilson.Web.Controllers
{

    public class InstallController : BaseController
    {
        private readonly UserManager<User> userManager;
        private readonly ILogger logger;
        private readonly IDatabaseSeeder dataSeeder;
        private readonly IRolesSeder rolesSeeder;
        private readonly IServiceScopeFactory services;

        public InstallController(
            UserManager<User> userManager, 
            ICompanyWorkData companyWorkData, 
            IMapper mapper, 
            ILoggerFactory loggerFactory,
            IDatabaseSeeder dataSeeder,
            IRolesSeder rolesSeeder,
            IServiceScopeFactory services) 
            : base(companyWorkData, mapper)
        {
            this.userManager = userManager;
            this.dataSeeder = dataSeeder;
            this.rolesSeeder = rolesSeeder;
            this.services = services;
            this.logger = loggerFactory.CreateLogger<InstallController>();
        }

        //
        // GET: /Install/InstallDatabase
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> InstallDatabase()
        {
            // Check again if a database was previously installed and return Error if true because in this case we should not
            // be here and don't give explanations about the error.
            var query = await this.CompanyWorkData.Settings.GetAllAsync();
            var settings = query.FirstOrDefault();
            if (settings != null)
            {
                if (!settings.IsDatabaseInstalled)
                {
                    return View("Error");
                }                
            }

            return View();
        }

        //
        // POST: /Install/InstallDatabase
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> InstallDatabase(InstallDatabaseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = this.Mapper.Map<InstallDatabaseViewModel, User>(model);

                // Set the Username!
                user.UserName = model.Email;

                // Create User.
                var result = await this.userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    this.logger.LogInformation(3, "Admin was created.");
                    this.rolesSeeder.Seed(this.services);
                    this.logger.LogInformation(3, "Roles were seeded into the database.");

                    // Make the user administrator.
                    await userManager.AddToRoleAsync(user, Constants.Roles.Administrator);

                    // Seed the database
                    if (model.SeedData)
                    {
                        this.dataSeeder.Seed(this.services);
                        this.logger.LogInformation(3, "Data was seeded into the database.");
                    }
                    
                    // Create home company.
                    var company = this.Mapper.Map<InstallDatabaseViewModel, Company>(model);
                    var companyAddress = this.Mapper.Map<InstallDatabaseViewModel, Address>(model);

                    // Set the company address and the shipping address.
                    company.AddressId = companyAddress.Id;
                    company.ShippingAddressId = companyAddress.Id;

                    this.CompanyWorkData.Companies.Add(company);
                    this.CompanyWorkData.Addresses.Add(companyAddress);

                    // Set database settings to installed and Save Home Company Id.
                    this.CompanyWorkData.Settings.Add(new Settings() { IsDatabaseInstalled = true, HomeCompanyId = company.Id });

                    // Save all changes.
                    await this.CompanyWorkData.CompleteAsync();

                    this.logger.LogInformation(3, "The database was installed successfully.");
                    return RedirectToAction(nameof(AccountController.Login), "Account");
                }

                this.AddErrors(result);
            }

            return View(model);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
