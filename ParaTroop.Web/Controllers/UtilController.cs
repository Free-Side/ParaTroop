using Microsoft.AspNetCore.Mvc;

namespace ParaTroop.Web.Controllers {
    [Route("api/util")]
    public class UtilController : Controller {
        [HttpGet("ip")]
        public IActionResult GetIp() {
            if (this.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)) {
                return Ok(new {
                    Address = forwardedFor[0].Split(',')[0],
                    ForwardedFor = forwardedFor
                });
            } else {
                return Ok(new {
                    Address = this.HttpContext.Connection.RemoteIpAddress.ToString()
                });
            }
        }
    }
}
