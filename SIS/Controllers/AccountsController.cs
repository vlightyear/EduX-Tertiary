using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Accounts;

namespace SIS.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AccountsController
        public async Task<IActionResult> AccountType()
        {
            var accounts = await _context.AccountTypes.ToListAsync();

            return View(accounts);
        }

        // GET: AccountsController/Create
        public ActionResult CreateAccountType()
        {
            return View();
        }

        // POST: AccountsController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAccountType(AccountType model)
        {
            var accounts = model;
            if (accounts != null)
            {
                accounts.CreatedAt = DateTime.Now;
                accounts.CreatedBy = User.Identity.Name;


                _context.Add(accounts);
                await _context.SaveChangesAsync();
                return RedirectToAction("AccountType");
            }
            else
            {
                // Log each error in ModelState to the console
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                    if (error.Exception != null)
                    {
                        Console.WriteLine($"Exception: {error.Exception.Message}");
                    }
                }
            }

            return View(accounts);
        }

        // GET: AccountsController/Edit/5
        public async Task<IActionResult> EditAccountType(int id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accounts = await _context.AccountTypes.FindAsync(id);
            if (accounts == null)
            {
                return NotFound();
            }
            return View(accounts);
        }

        // POST: AccountsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditAccountType(AccountType model)
        {
            var accounts = model;
            if (accounts != null)
            {
                try
                {
                    accounts.UpdatedAt = DateTime.Now;
                    accounts.UpdatedBy = User.Identity.Name;

                    _context.Update(accounts);

                    await _context.SaveChangesAsync();
                }
                catch (Exception)
                {
                    return View(accounts);

                }
                return RedirectToAction("AccountType");
            }

            return View(accounts);
        }

        // GET: AccountsController/Delete/5
        public ActionResult DeleteAccountType(int id)
        {
            return View();
        }

        // POST: AccountsController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAccountType(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
