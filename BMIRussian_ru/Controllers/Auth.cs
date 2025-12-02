using BMIRussian_ru.Exceptions;
using BMIRussian_ru.Logic;
using BMIRussian_ru.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Controllers
{
    [ApiController]
    public class AuthController(AuthLogic logic) : ControllerBase
    {
        [HttpPost]
        [EnableCors("APIPolicy")]
        [Route("/auth/fromtelegrambot")]
        public virtual IActionResult FromTelegramBot([FromBody] FromTelegramBotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TemporaryToken) || string.IsNullOrWhiteSpace(request.TelegramId))
            {
                return BadRequest("TemporaryToken and TelegramId are required");
            }

            try
            {
                string bearerToken = logic.AuthenticateFromTelegramBot(request);
                return Ok(bearerToken);
            }
            catch (UserNotFoundException)
            {
                return Unauthorized();
            }
            catch (InvalidTokenException)
            {
                return Unauthorized();
            }
            catch (TokenExpiredException)
            {
                return Unauthorized("Temporary token has expired");
            }
        }
    }
}
