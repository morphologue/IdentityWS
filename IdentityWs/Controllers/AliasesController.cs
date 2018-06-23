using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IdentityWs.Jobs;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityWs.Controllers
{
    public class AliasesController : Controller
    {
        IdentityWsDbContext ef;
        ILogger<AliasesController> log;
        IUtcNow now;
        IBackgroundJobRunner<EmailQueueProcessor> runner;

        public AliasesController(IdentityWsDbContext ef, ILogger<AliasesController> log, IUtcNow now,
            IBackgroundJobRunner<EmailQueueProcessor> runner)
        {
            this.ef = ef;
            this.log = log;
            this.now = now;
            this.runner = runner;
        }

        // Get the alias's confirmation token, or null if it has already been confirmed.
        public async Task<IActionResult> Index([EmailAddress, MaxLength(100)] string email_address)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Alias alias = await ef.Aliases.FirstOrDefaultAsync(a => a.EmailAddress == email_address);
            if (alias == null)
                return NotFound();

            return Json(new Dictionary<string, string>()
            {
                ["confirmToken"] = alias.DateConfirmed.HasValue ? null : alias.ConfirmationToken
            });
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

            return NoContent();
        }

        // Change the password of the being of an existing alias.
        public class IndexPatchRequestBody
        {
            public string resetToken { get; set; }
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

            // Get the being.
            Being being = (await ef.Aliases
                .Include(a => a.Being)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being;
            if (being == null)
                return NotFound();

            if (body.resetToken != null) {
                // Authenticate via reset token.
                if (body.oldPassword != null)
                    // Only one of resetToken or oldPassword may be supplied.
                    return BadRequest();
                if (!being.PasswordResetTokenValidUntil.HasValue || being.PasswordResetTokenValidUntil <= now.UtcNow
                        || being.PasswordResetToken != body.resetToken)
                    return Unauthorized();
                if (Sha512Util.TestPassword(body.password, being.SaltedHashedPassword))
                    // Cannot change password to itself.
                    return StatusCode(StatusCodes.Status409Conflict);
                // The token is used up.
                being.PasswordResetToken = null;
                being.PasswordResetTokenValidUntil = null;
            } else {
                // Authenticate via old password.
                if (body.oldPassword == null)
                    // One of resetToken or oldPassword must be supplied.
                    return BadRequest();
                if (!Sha512Util.TestPassword(body.oldPassword, being.SaltedHashedPassword))
                    return Unauthorized();
                if (body.oldPassword == body.password)
                    // Cannot change password to itself.
                    return StatusCode(StatusCodes.Status409Conflict);
            }

            // Change the password.
            being.SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(body.password);
            await ef.SaveChangesAsync();

            return NoContent();
        }

        // Remove this alias from the being, provided that at least one alias remains. If, on the
        // other hand you want to the entire being, delete all of its clients.
        [HttpDelete]
        [ActionName("Index")]
        public async Task<IActionResult> IndexDelete([EmailAddress, MaxLength(100)] string email_address)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Alias alias = (await ef.Aliases
                .Include(a => a.Being).ThenInclude(b => b.Aliases)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address));
            Being being = alias?.Being;
            if (being == null)
                return NotFound();

            if (being.Aliases.Count < 2)
                return StatusCode(StatusCodes.Status403Forbidden);

            ef.Aliases.Remove(alias);
            await ef.SaveChangesAsync();
            return NoContent();
        }

        // Generate a password reset token, invalidating any previous such tokens. The token will be
        // valid for 1 hour.
        [HttpPost]
        [ActionName("Reset")]
        public async Task<IActionResult> ResetPost([EmailAddress, MaxLength(100)] string email_address)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Being being = (await ef.Aliases
                .Include(a => a.Being)
                .FirstOrDefaultAsync(a => a.EmailAddress == email_address))
                ?.Being;
            if (being == null)
                return NotFound();

            being.PasswordResetToken = Guid.NewGuid().ToString();
            being.PasswordResetTokenValidUntil = now.UtcNow.AddHours(1);
            await ef.SaveChangesAsync();

            return Json(new Dictionary<string, string>()
            {
                ["resetToken"] = being.PasswordResetToken
            });
        }

        // Receive back a confirmation token obtained from Index() above.
        public class ConfirmPostRequestBody
        {
            [Required]
            public string confirmToken { get; set; }
        }
        [HttpPost]
        [ActionName("Confirm")]
        public async Task<IActionResult> ConfirmPost([EmailAddress, MaxLength(100)] string email_address, [FromBody] ConfirmPostRequestBody body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Alias alias = await ef.Aliases.FirstOrDefaultAsync(a => a.EmailAddress == email_address);
            if (alias == null)
                return NotFound();
            
            if (alias.ConfirmationToken != body.confirmToken)
                return Unauthorized();

            if (!alias.DateConfirmed.HasValue) {
                alias.DateConfirmed = now.UtcNow;
                await ef.SaveChangesAsync();
            }

            return NoContent();
        }

        public class EmailRequestBody
        {
            [EmailAddress]
            [Required]
            [MaxLength(100)]
            public string from { get; set; }

            [EmailAddress]
            [MaxLength(100)]
            public string replyTo { get; set; }

            [Required]
            [MaxLength(100)]
            public string subject { get; set; }

            public string bodyText { get; set; }

            public string bodyHTML { get; set; }

            // Default = false
            public bool? sendIfUnconfirmed { get; set; }
        }
        [HttpPost]
        public async Task<IActionResult> Email([EmailAddress, MaxLength(100)] string email_address, [FromBody] EmailRequestBody body)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Get the alias.
            Alias alias = await ef.Aliases.FirstOrDefaultAsync(a => a.EmailAddress == email_address);
            if (alias == null)
                return NotFound();

            // Enqueue the email.
            ef.Emails.Add(new Email
            {
                AliasID = alias.AliasID,
                From = body.from,
                ReplyTo = body.replyTo,
                Subject = body.subject,
                BodyText = body.bodyText,
                BodyHTML = body.bodyHTML,
                SendIfUnconfirmed = body.sendIfUnconfirmed ?? false
            });
            await ef.SaveChangesAsync();

            // Ensure the email is sent sooner rather than later.
            runner.Nudge();

            return NoContent();
        }
    }
}
