using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PlayerRatings.Controllers;
using PlayerRatings.Localization;
using PlayerRatings.Models;

namespace PlayerRatings.Services
{
    public class InvitesService : IInvitesService
    {
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IStringLocalizer<InvitesService> _localizer;

        public InvitesService(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IEmailSender emailSender, IHttpContextAccessor httpContextAccessor,
            IStringLocalizer<InvitesService> localizer)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _httpContextAccessor = httpContextAccessor;
            _localizer = localizer;
        }

        public string GetInviteUrl(Guid inviteId, IUrlHelper urlHelper)
        {
            return urlHelper.Action(nameof(AccountController.Register), "Account", new {inviteId},
                _httpContextAccessor.HttpContext.Request.Scheme);
        }

        public async Task SendEmail(Invite invite, IUrlHelper urlHelper)
        {
            var callbackUrl = GetInviteUrl(invite.Id, urlHelper);
            var title = _localizer[nameof(LocalizationKey.InvitedYou), invite.InvitedBy.DisplayName];
            var msg = _localizer[nameof(LocalizationKey.ConfirmAccount), callbackUrl];
            await _emailSender.SendEmailAsync(invite.CreatedUser.Email, title, msg);
        }

        public async Task<ApplicationUser> Invite(string email, string displayName, string ranking, 
            string rankingOrganization, DateTimeOffset? rankingDate,
            string residence, int? birthYearValue, string photoUrl,
            ApplicationUser invitedBy, League league, IUrlHelper urlHelper)
        {
            var invited = await _userManager.FindByEmailAsync(email);

            Invite invitation = null;
            if (invited == null)
            {
                var user = new ApplicationUser
                {
                    DisplayName = displayName,
                    UserName = email,
                    Email = email,
                    Residence = !string.IsNullOrEmpty(residence) ? residence : "Singapore",
                    BirthYearValue = birthYearValue,
                    Photo = photoUrl
                };
                var result = await _userManager.CreateAsync(user, Guid.NewGuid() + "Aa1!");

                if (!result.Succeeded)
                {
                    throw new Exception(_localizer[nameof(LocalizationKey.ErrorOccurred)]);
                }

                // Add initial ranking to PlayerRanking table if provided
                if (!string.IsNullOrEmpty(ranking))
                {
                    var playerRanking = CreatePlayerRanking(user.Id, ranking, rankingOrganization, rankingDate);
                    if (playerRanking != null)
                    {
                        _context.PlayerRankings.Add(playerRanking);
                    }
                }

                invitation = new Invite
                {
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTimeOffset.Now,
                    CreatedUser = user,
                    InvitedBy = invitedBy
                };

                _context.Invites.Add(invitation);
                invited = user;
            }

            if (league != null && !_context.LeaguePlayers.Any(lp => lp.LeagueId == league.Id && lp.UserId == invited.Id))
            {
                _context.LeaguePlayers.Add(new LeaguePlayer
                {
                    Id = Guid.NewGuid(),
                    League = league,
                    User = invited
                });
            }

            _context.SaveChanges();

            if (invitation != null)
            {
                _ = SendEmail(invitation, urlHelper);
            }

            return invited;
        }

        /// <summary>
        /// Create a PlayerRanking entry with explicit organization and date
        /// </summary>
        private PlayerRanking CreatePlayerRanking(string userId, string rankingString, string organization, DateTimeOffset? rankingDate)
        {
            if (string.IsNullOrEmpty(rankingString))
                return null;

            // Extract just the grade (e.g., "1D", "2K", "9P")
            var gradeMatch = Regex.Match(rankingString, @"(\d+[DKP])", RegexOptions.IgnoreCase);
            string ranking = gradeMatch.Success ? gradeMatch.Groups[1].Value.ToUpper() : rankingString.ToUpper();

            // Default to SWA if organization not specified
            string org = string.IsNullOrEmpty(organization) || organization == "Other" ? "SWA" : organization;

            return new PlayerRanking
            {
                RankingId = Guid.NewGuid(),
                PlayerId = userId,
                Ranking = ranking,
                Organization = org == "Other" ? null : org,
                RankingDate = rankingDate,
                RankingNote = "Initial ranking"
            };
        }
    }
}
