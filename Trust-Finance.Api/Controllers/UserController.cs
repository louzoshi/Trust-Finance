namespace TF.Controllers
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.AspNetCore.Mvc;
    using TF.Models;
    using TF.Data;
    using TF.ViewModels;
    using TF.Extensions;
    using Microsoft.AspNetCore.Authorization;

    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [Authorize(Roles = "admin")]
        [HttpGet("users")]
        public async Task<IActionResult> GetAsync(
            [FromServices] TFDataContext context)
        {
            try
            {
                var users = await context.Users.ToListAsync();
                return Ok(new ResultViewModel<List<User>>(users));
            }
            catch
            {
                return StatusCode(500, new ResultViewModel<List<User>>("Internal server error"));
            }
        }

        [Authorize(Roles = "admin")]
        [HttpGet("users/{id:int}")]
        public async Task<IActionResult> GetByIdAsync(
            [FromRoute] int id,
            [FromServices] TFDataContext context)
        {
            try
            {
                var user = await context
                    .Users
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (user == null)
                    return NotFound(new ResultViewModel<User>("User not found"));

                return Ok(new ResultViewModel<User>(user));
            }
            catch
            {
                return StatusCode(500, new ResultViewModel<User>("Internal server error"));
            }
        }

        [Authorize(Roles = "admin")]
        [HttpPut("users/{id:int}")]
        public async Task<IActionResult> PutAsync(
            [FromRoute] int id,
            [FromBody] RegisterUserViewModel model,
            [FromServices] TFDataContext context)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ResultViewModel<User>(ModelState.GetErrors()));

            try
            {
                var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id);
                if (user == null)
                    return NotFound(new ResultViewModel<User>("User not found"));

                user.Name = model.Name;
                user.Email = model.Email;
                user.Image = model.Image;
                user.Slug = model.Slug;

                context.Users.Update(user);
                await context.SaveChangesAsync();

                return Ok(new ResultViewModel<User>(user));
            }
            catch (DbUpdateException)
            {
                return StatusCode(400, new ResultViewModel<User>("Could not update the user"));
            }
            catch
            {
                return StatusCode(500, new ResultViewModel<User>("Internal server error"));
            }
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] int id,
            [FromServices] TFDataContext context)
        {
            try
            {
                var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id);
                if (user == null)
                    return NotFound(new ResultViewModel<User>("User not found"));

                context.Users.Remove(user);
                await context.SaveChangesAsync();

                return Ok(new ResultViewModel<User>(user));
            }
            catch (DbUpdateException)
            {
                return StatusCode(400, new ResultViewModel<User>("Could not delete the user"));
            }
            catch
            {
                return StatusCode(500, new ResultViewModel<User>("Internal server error"));
            }
        }
    }
}
