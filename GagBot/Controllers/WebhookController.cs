﻿using GagBot.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace GagBot.Controllers
{
    public class WebhookController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromServices] HandleUpdateService handleUpdateService,
                                              [FromBody] Update update)
        {
            // await handleUpdateService.
            return Ok();
        }
    }
}
