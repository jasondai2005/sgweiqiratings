using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PlayerRatings.Models;
using PlayerRatings.Repositories;
using PlayerRatings.Util;

namespace PlayerRatings.Infrastructure.Attributes
{
    /// <summary>
    /// Authorization filter that ensures the current user is an admin of the specified league.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class LeagueAdminAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _leagueIdParameter;

        /// <summary>
        /// Creates a new LeagueAdminAttribute.
        /// </summary>
        /// <param name="leagueIdParameter">Name of the route/query parameter containing the league ID. Defaults to "leagueId".</param>
        public LeagueAdminAttribute(string leagueIdParameter = "leagueId")
        {
            _leagueIdParameter = leagueIdParameter;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get the league ID from the action arguments
            if (!context.ActionArguments.TryGetValue(_leagueIdParameter, out var leagueIdObj))
            {
                // Try to get from route data
                if (!context.RouteData.Values.TryGetValue(_leagueIdParameter, out leagueIdObj))
                {
                    context.Result = new BadRequestObjectResult($"Missing {_leagueIdParameter} parameter");
                    return;
                }
            }

            Guid leagueId;
            if (leagueIdObj is Guid guid)
            {
                leagueId = guid;
            }
            else if (leagueIdObj is string str && Guid.TryParse(str, out var parsed))
            {
                leagueId = parsed;
            }
            else if (leagueIdObj != null && leagueIdObj.GetType() == typeof(Guid?))
            {
                var nullableGuid = (Guid?)leagueIdObj;
                if (!nullableGuid.HasValue)
                {
                    context.Result = new BadRequestObjectResult($"Invalid {_leagueIdParameter} parameter");
                    return;
                }
                leagueId = nullableGuid.Value;
            }
            else
            {
                context.Result = new BadRequestObjectResult($"Invalid {_leagueIdParameter} parameter");
                return;
            }

            // Get required services
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var leaguesRepository = context.HttpContext.RequestServices.GetRequiredService<ILeaguesRepository>();

            // Get current user
            var currentUser = await context.HttpContext.User.GetApplicationUser(userManager);
            if (currentUser == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check if user is admin
            var league = leaguesRepository.GetAdminAuthorizedLeague(currentUser, leagueId);
            if (league == null)
            {
                context.Result = new ForbidResult();
                return;
            }

            // Store the league in HttpContext.Items for use in the action
            context.HttpContext.Items["AuthorizedLeague"] = league;
            context.HttpContext.Items["CurrentUser"] = currentUser;

            await next();
        }
    }

    /// <summary>
    /// Authorization filter that ensures the current user is a member of the specified league.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class LeagueMemberAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _leagueIdParameter;

        /// <summary>
        /// Creates a new LeagueMemberAttribute.
        /// </summary>
        /// <param name="leagueIdParameter">Name of the route/query parameter containing the league ID. Defaults to "leagueId".</param>
        public LeagueMemberAttribute(string leagueIdParameter = "leagueId")
        {
            _leagueIdParameter = leagueIdParameter;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get the league ID from the action arguments
            if (!context.ActionArguments.TryGetValue(_leagueIdParameter, out var leagueIdObj))
            {
                // Try to get from route data
                if (!context.RouteData.Values.TryGetValue(_leagueIdParameter, out leagueIdObj))
                {
                    context.Result = new BadRequestObjectResult($"Missing {_leagueIdParameter} parameter");
                    return;
                }
            }

            Guid leagueId;
            if (leagueIdObj is Guid guid)
            {
                leagueId = guid;
            }
            else if (leagueIdObj is string str && Guid.TryParse(str, out var parsed))
            {
                leagueId = parsed;
            }
            else if (leagueIdObj != null && leagueIdObj.GetType() == typeof(Guid?))
            {
                var nullableGuid = (Guid?)leagueIdObj;
                if (!nullableGuid.HasValue)
                {
                    context.Result = new BadRequestObjectResult($"Invalid {_leagueIdParameter} parameter");
                    return;
                }
                leagueId = nullableGuid.Value;
            }
            else
            {
                context.Result = new BadRequestObjectResult($"Invalid {_leagueIdParameter} parameter");
                return;
            }

            // Get required services
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var leaguesRepository = context.HttpContext.RequestServices.GetRequiredService<ILeaguesRepository>();

            // Get current user
            var currentUser = await context.HttpContext.User.GetApplicationUser(userManager);
            if (currentUser == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check if user is a member
            var league = leaguesRepository.GetUserAuthorizedLeague(currentUser, leagueId);
            if (league == null)
            {
                context.Result = new ForbidResult();
                return;
            }

            // Store the league in HttpContext.Items for use in the action
            context.HttpContext.Items["AuthorizedLeague"] = league;
            context.HttpContext.Items["CurrentUser"] = currentUser;

            await next();
        }
    }

    /// <summary>
    /// Extension methods to retrieve data stored by authorization attributes.
    /// </summary>
    public static class LeagueAuthorizationExtensions
    {
        /// <summary>
        /// Gets the authorized league from HttpContext.Items (set by LeagueAdminAttribute or LeagueMemberAttribute).
        /// </summary>
        public static League GetAuthorizedLeague(this Controller controller)
        {
            return controller.HttpContext.Items["AuthorizedLeague"] as League;
        }

        /// <summary>
        /// Gets the current user from HttpContext.Items (set by LeagueAdminAttribute or LeagueMemberAttribute).
        /// </summary>
        public static ApplicationUser GetCurrentUser(this Controller controller)
        {
            return controller.HttpContext.Items["CurrentUser"] as ApplicationUser;
        }
    }
}

