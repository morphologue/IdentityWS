using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IdentityWS.Models;
using IdentityWS.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityWS.Controllers
{
    public class AliasesController : Controller
    {
        IdentityWsDbContext ef;
        ILogger<AliasesController> log;

        public AliasesController(IdentityWsDbContext ef, ILogger<AliasesController> log) {
            this.ef = ef;
            this.log = log;
        }

        // Return an HTTP status code indicating whether the alias exists for any app.
        public async Task<IActionResult> Index(string email_address) {
            if (await ef.Aliases.AnyAsync(a => a.EmailAddress == email_address))
                return Ok();
            return NotFound();
        }

        // Create an alias. When creating, either 'otherEmailAddress' can be supplied to link the
        // new alias to an existing being, or 'password' can be supplied to create a new being.
        public class IndexPostRequestBody
        {
            public string otherEmailAddress { get; set; }
            [MinLength(7)]
            public string password { get; set; }
        }
        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPost([EmailAddress, MaxLength(100)] string email_address,
            [FromBody] IndexPostRequestBody body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Make sure the alias doesn't already exist.
            if (await ef.Aliases.AnyAsync(a => a.EmailAddress == email_address))
                return StatusCode(StatusCodes.Status409Conflict);

            // Create or identify a being.
            Being being;
            if (body.password != null) {
                // A new being should be created.
                if (body.otherEmailAddress != null)
                    // Both 'password' and 'otherEmailAddress' cannot be provided.
                    return BadRequest();
                ef.Beings.Add(being = new Being
                {
                    SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(body.password)
                });
                await ef.SaveChangesAsync();  // To populate BeingID
            } else {
                // The new alias should be linked to an existing being. 
                if (body.otherEmailAddress == null)
                    // One of 'password' and 'otherEmailAddress' must be provided.
                    return BadRequest();
                being = (await ef.Aliases
                    .Include(a => a.Being)
                    .FirstOrDefaultAsync(a => a.EmailAddress == body.otherEmailAddress))
                    ?.Being;
                if (being == null) {
                    log.LogWarning($"Invalid attempt to link '{email_address}' to non-existent alias '{body.otherEmailAddress}'");
                    return NotFound();
                }
            }

            // Create the alias.
            ef.Aliases.Add(new Alias
            {
                EmailAddress = email_address,
                BeingID = being.BeingID
            });
            await ef.SaveChangesAsync();

            return Ok();
        }

        // Change the password of the being of an existing alias.
        public class IndexPatchRequestBody
        {
            [Required]
            public string oldPassword { get; set; }
            [MinLength(7)]
            [Required]
            public string password { get; set; }
        }
        [HttpPatch]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPatch([EmailAddress, MaxLength(100)] string email_address,
            [FromBody] IndexPatchRequestBody body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (body.password == body.oldPassword)
                return BadRequest();

            // Get the being.
            Being being = (await ef.Aliases
                .Include(a => a.Being)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being;
            if (being == null)
                return NotFound();
            
            // Test the existing password.
            if (!Sha512Util.TestPassword(body.oldPassword, being.SaltedHashedPassword))
                return Unauthorized();

            // Change the password.
            being.SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(body.password);
            await ef.SaveChangesAsync();

            return Ok();
        }
    }
}
