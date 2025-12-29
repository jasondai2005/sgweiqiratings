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

        public async Task<ApplicationUser> Invite(string email, string displayName, string ranking, ApplicationUser invitedBy, League league, IUrlHelper urlHelper)
        {
            var invited = await _userManager.FindByEmailAsync(email);

            Invite invitation = null;
            if (invited == null)
            {
                var user = new ApplicationUser
                {
                    DisplayName = displayName,
                    UserName = email,
                    Email = email
                };
                var result = await _userManager.CreateAsync(user, Guid.NewGuid() + "Aa1!");

                if (!result.Succeeded)
                {
                    throw new Exception(_localizer[nameof(LocalizationKey.ErrorOccurred)]);
                }

                // Add initial ranking to PlayerRanking table if provided
                if (!string.IsNullOrEmpty(ranking))
                {
                    var playerRanking = ParseAndCreatePlayerRanking(user.Id, ranking);
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
        /// Parse ranking string and create a PlayerRanking entry.
        /// Supports formats: "1D" (SWA), "(1D)" (TGA), "[1D CWA]" (Foreign)
        /// </summary>
        private PlayerRanking ParseAndCreatePlayerRanking(string userId, string rankingString)
        {
            if (string.IsNullOrEmpty(rankingString))
                return null;

            string ranking;
            string organization;

            // TGA format: (1D) or (1K)
            if (rankingString.StartsWith("(") && rankingString.Contains(")"))
            {
                var match = Regex.Match(rankingString, @"\((\d+[DKP])\)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    ranking = match.Groups[1].Value.ToUpper();
                    organization = "TGA";
                }
                else
                {
                    return null;
                }
            }
            // Foreign format: [1D CWA] or [China 5D]
            else if (rankingString.StartsWith("[") && rankingString.Contains("]"))
            {
                var content = rankingString.Trim('[', ']');
                var parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 2)
                {
                    var rankingMatch = parts.FirstOrDefault(p => Regex.IsMatch(p, @"^\d+[DKP]$", RegexOptions.IgnoreCase));
                    if (rankingMatch != null)
                    {
                        ranking = rankingMatch.ToUpper();
                        organization = string.Join(" ", parts.Where(p => p != rankingMatch));
                    }
                    else
                    {
                        ranking = parts[0].ToUpper();
                        organization = string.Join(" ", parts.Skip(1));
                    }
                }
                else
                {
                    ranking = content.ToUpper();
                    organization = "Foreign";
                }
            }
            // SWA format: 1D, 2K, etc.
            else
            {
                ranking = rankingString.ToUpper();
                organization = "SWA";
            }

            // Extract just the grade
            var gradeMatch = Regex.Match(ranking, @"(\d+[DKP])", RegexOptions.IgnoreCase);
            if (gradeMatch.Success)
            {
                ranking = gradeMatch.Groups[1].Value.ToUpper();
            }

            return new PlayerRanking
            {
                RankingId = Guid.NewGuid(),
                PlayerId = userId,
                Ranking = ranking,
                Organization = organization,
                RankingDate = null, // Date unknown for new invites
                RankingNote = "Initial ranking"
            };
        }
    }
}
