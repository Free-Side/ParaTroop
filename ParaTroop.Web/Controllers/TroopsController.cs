using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ParaTroop.Web.Data;
using ParaTroop.Web.Models;
using ParaTroop.Web.Services;

namespace ParaTroop.Web.Controllers {
    [Route("api/troops")]
    public class TroopsController : Controller {
        private readonly TroopDbContext dbContext;

        public TroopsController(TroopDbContext dbContext) {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken) {
            return Ok(
                await this.dbContext.Troops.OrderByDescending(t => t.Created)
                    .ToListAsync(cancellationToken: cancellationToken)
            );
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(
            Int64 id,
            CancellationToken cancellationToken) {

            var troop = await this.dbContext.Troops.SingleOrDefaultAsync(
                t => t.Id == id,
                cancellationToken: cancellationToken
            );

            if (troop != null) {
                return Ok(troop);
            } else {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateTroopModel troop,
            CancellationToken cancellationToken) {

            var status = await TroopConnection.TestConnection(troop.Hostname, troop.Port, troop.PasswordHash, cancellationToken);
            if (status != TroopConnectionStatus.Success) {
                return BadRequest(new ProblemDetails {
                    Detail = $"Unable to connect to Troop server: {status}.",
                    Status = 400,
                    Type = ProblemTypes.InvalidTroopConnectionInfo
                });
            }

            var newTroop = new Troop(
                0,
                troop.Name,
                troop.Hostname,
                troop.Port,
                DateTime.UtcNow
            );

            try {
                var createdTroop =
                    await this.dbContext.Troops.AddAsync(newTroop, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                return CreatedAtAction(nameof(Get), new { id = createdTroop.Entity.Id }, createdTroop.Entity);
            } catch (DbUpdateException ex) {
                if (ex.InnerException is SqliteException sqlEx && sqlEx.SqliteErrorCode == 19) {
                    // Unique Constraint Failure
                    return BadRequest(new ProblemDetails {
                        Detail = $"The specified Troop Name is already in use: {troop.Name}.",
                        Status = 400,
                        Type = ProblemTypes.TroopNameAlreadyInUse
                    });
                }

                throw;
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(
            Int64 id,
            CancellationToken cancellationToken) {

            var troop = await this.dbContext.Troops.SingleOrDefaultAsync(
                t => t.Id == id,
                cancellationToken: cancellationToken
            );

            if (troop != null) {
                this.dbContext.Troops.Remove(troop);
            }

            await this.dbContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }
    }
}
