using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dixels.Portal.Web.Controllers;

[ApiController]
[Route("api/demo")]
[AllowAnonymous]
public class DemoController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult<object> Ping()
    {
        return Ok(new
        {
            message = "Hello from the Dixels.Portal ABP backend!",
            timestampUtc = DateTime.UtcNow
        });
    }
}
