using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TF.Models;
using TF.Services;
using TF.ViewModels;

namespace TF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserViewModel model,
        [FromServices] AccountService service)
    {
        try
        {
            var user = await service.RegisterAsync(model);
            return Created($"api/users/{user.Id}", new ResultViewModel<User>(user));
        }
        catch (InvalidOperationException e)
        {
            // Broken business rule (e.g. the email is already taken).
            return BadRequest(new ResultViewModel<User>(e.Message));
        }
        catch (DbUpdateException e)
        {
            return BadRequest(new ResultViewModel<User>(e.Message));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
       [FromBody] LoginViewModel model,
       [FromServices] AccountService service,
       [FromServices] TokenService tokenService)
    {
        try
        {
            var token = await service.LoginAsync(model, tokenService);
            // Explicit errors argument: with T == string the single-argument
            // call binds to the (string error) overload and puts the token
            // in Errors instead of Data.
            return Ok(new ResultViewModel<string>(token, new List<string>()));
        }
        catch (UnauthorizedAccessException e)
        {
            return Unauthorized(new ResultViewModel<string>(e.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ResultViewModel<string>("Internal error while signing in"));
        }
    }
}
