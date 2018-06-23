using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityWs.Controllers
{
    public class ClientsController : Controller
    {
        IdentityWsDbContext ef;
        ILogger<ClientsController> log;
        IUtcNow now;
        IConfiguration config;

        public ClientsController(IdentityWsDbContext ef, ILogger<ClientsController> log, IUtcNow now, IConfiguration config)
        {
            this.ef = ef;
            this.log = log;
            this.now = now;
            this.config = config;
        }

        // Return the client data associated with the given being and client name.
        public async Task<IActionResult> Index([EmailAddress, MaxLength(100)] string email_address, [Required, MaxLength(20)] string client)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            BeingClient bc = (await ef.Aliases
                .Include(a => a.Being).ThenInclude(b => b.Clients)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being
                .Clients
                .FirstOrDefault(c => c.ClientName == client);
            if (bc == null)
                return NotFound();

            Dictionary<string, string> data = new Dictionary<string, string>();
            foreach (BeingClientDatum datum in await ef.BeingClientData
                    .Where(d => d.BeingClientID == bc.BeingClientID)
                    .ToListAsync())
                data.Add(datum.Key, datum.Value);
            return Json(data);
        }

        // Register a client against the given being and client name.
        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPost([Required, EmailAddress, MaxLength(100)] string email_address,
            [Required, MaxLength(20)] string client, [FromBody] Dictionary<string, string> body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Being being = (await ef.Aliases
                .Include(a => a.Being).ThenInclude(b => b.Clients)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being;
            if (being == null)
                return NotFound();
            if (being.Clients.Any(c => c.ClientName == client))
                return StatusCode(StatusCodes.Status409Conflict);

            being.Clients.Add(new BeingClient
            {
                ClientName = client,
                Data = body.Select(kv => new BeingClientDatum
                {
                    Key = kv.Key,
                    Value = kv.Value
                }).ToList()
            });
            await ef.SaveChangesAsync();

            return NoContent();
        }

        // Delete the given client from the being. If no clients remain, also delete the entire
        // being.
        [HttpDelete]
        [ActionName("Index")]
        public async Task<IActionResult> IndexDelete([Required, EmailAddress, MaxLength(100)] string email_address,
            [Required, MaxLength(20)] string client)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Being being = (await ef.Aliases
                .Include(a => a.Being).ThenInclude(b => b.Clients)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being;
            if (being == null)
                return NotFound();
            BeingClient bc = being.Clients.FirstOrDefault(c => c.ClientName == client);
            if (bc == null)
                return NotFound();

            if (being.Clients.Count == 1)
                ef.Beings.Remove(being);
            else
                ef.BeingClients.Remove(bc);
            await ef.SaveChangesAsync();
            return NoContent();
        }

        public class LoginRequestBody
        {
            [Required]
            public string password { get; set; }
        }
        [HttpPost]
        public async Task<IActionResult> Login([EmailAddress, MaxLength(100)] string email_address, [Required, MaxLength(20)] string client,
            [FromBody] LoginRequestBody body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Get the entities.
            Alias alias = await ef.Aliases
                .Include(a => a.Being).ThenInclude(b => b.Clients)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address);
            Being being = alias?.Being;
            if (being == null || !being.Clients.Any(c => c.ClientName == client))
                return NotFound();

            // Check the number of consecutive failures.
            DateTime period_start = now.UtcNow.AddMinutes(-1 * config.GetValue<double>("LockoutPeriodMins"));
            int consecutive_failures = await ef.LoginAttempts
                .Where(a => a.Alias.BeingID == being.BeingID
                    && a.DateCreated >= period_start
                    && !a.Success
                    && !ef.LoginAttempts.Any(a2 => a2.Alias.BeingID == being.BeingID
                        && a2.LoginAttemptID > a.LoginAttemptID
                        && a2.Success))
                .CountAsync();
            if (consecutive_failures >= config.GetValue<int>("MaxFailedLoginsBeforeLockout"))
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            // Check the password.
            bool password_ok = Sha512Util.TestPassword(body.password, being.SaltedHashedPassword);

            // Log the attempt.
            ef.LoginAttempts.Add(new LoginAttempt
            {
                AliasID = alias.AliasID,
                Success = password_ok,
                ClientName = client
            });
            await ef.SaveChangesAsync();

            return password_ok ? (IActionResult)NoContent() : Unauthorized();
        }
    }
}
